using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace AllSharpReports
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<FilterParameter> filterParameters;
        private ObservableCollection<SavedQuery> savedQueries = new ObservableCollection<SavedQuery>();

        public class FilterParameter : INotifyPropertyChanged
        {
            private string _field;
            private string _operator;
            private string _value;

            public List<string> Operators { get; } = new List<string> { "=", "<", ">", "<=", ">=", "CONTAINS" };

            public string Field
            {
                get { return _field; }
                set
                {
                    if (_field != value)
                    {
                        _field = value;
                        OnPropertyChanged(nameof(Field));
                    }
                }
            }

            public string Operator
            {
                get { return _operator; }
                set
                {
                    if (_operator != value)
                    {
                        _operator = value;
                        OnPropertyChanged(nameof(Operator));
                    }
                }
            }

            public string Value
            {
                get { return _value; }
                set
                {
                    if (_value != value)
                    {
                        _value = value;
                        OnPropertyChanged(nameof(Value));
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        [Serializable]
        public class SavedQuery
        {
            public string Name { get; set; }
            public string Query { get; set; }
        }
        [Serializable]
        public class Credentials
        {
            public string ServerAddress { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string DatabaseName { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            
            filterParameters = new ObservableCollection<FilterParameter>();
            filterFieldsPanel.DataContext = filterParameters;
            LoadCredentials();
            LoadSavedQueries();  // Загрузка сохраненных запросов
        }

        private void SaveCredentialsButton_Click(object sender, RoutedEventArgs e)
        {
            Credentials credentials = new Credentials
            {
                ServerAddress = txtServerAddress.Text,
                Username = txtUsername.Text,
                Password = txtPassword.Password,
                DatabaseName = txtDatabaseName.Text
            };

            SaveCredentials(credentials);
        }

        private void CheckQueryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем корректность SQL запроса
                using (MySqlConnection connection = new MySqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (MySqlCommand command = new MySqlCommand(txtSqlQuery.Text, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                // Генерируем фильтры
                GenerateFilters();

                // Установка DataContext для filterFieldsPanel
                filterFieldsPanel.DataContext = filterParameters;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking query: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            MessageBox.Show("Success");
        }


        private void CreateReportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Генерируем отчет
                GenerateReport();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCredentials(Credentials credentials)
        {
            try
            {
                using (FileStream fs = new FileStream("credentials.dat", FileMode.Create))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(fs, credentials);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCredentials()
        {
            try
            {
                if (File.Exists("credentials.dat"))
                {
                    using (FileStream fs = new FileStream("credentials.dat", FileMode.Open))
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        Credentials credentials = (Credentials)formatter.Deserialize(fs);

                        txtServerAddress.Text = credentials.ServerAddress;
                        txtUsername.Text = credentials.Username;
                        txtPassword.Password = credentials.Password;
                        txtDatabaseName.Text = credentials.DatabaseName;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetConnectionString()
        {
            return $"Server={txtServerAddress.Text};Database={txtDatabaseName.Text};User ID={txtUsername.Text};Password={txtPassword.Password};CharSet=utf8mb3;";
        }

        private void GenerateFilters()
        {
            filterParameters.Clear();

            // Извлекаем поля из SELECT части SQL запроса
            string pattern = @"SELECT(.+?)FROM";
            Match match = Regex.Match(txtSqlQuery.Text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                string selectFields = match.Groups[1].Value;

                // Разделяем поля по запятой, учитывая, что ',' внутри функций (например, CONCAT) не являются разделителями
                string[] fields = Regex.Split(selectFields, @",(?![^\(]*\))");

                foreach (string field in fields)
                {
                    // Извлекаем имя поля
                    string cleanedField = Regex.Replace(field, @"\s+AS\s+\S+$", string.Empty).Trim();

                    // Если поле содержит CONCAT, то обработаем его особо
                    if (cleanedField.Contains("CONCAT"))
                    {
                        // Извлекаем все аргументы CONCAT
                        MatchCollection concatArgs = Regex.Matches(cleanedField, @"CONCAT\(([^)]+)\)");
                        foreach (Match concatArg in concatArgs)
                        {
                            string[] argFields = concatArg.Groups[1].Value.Split(',');
                            foreach (string argField in argFields)
                            {
                                string cleanedArgField = argField.Trim();
                                if (!string.IsNullOrEmpty(cleanedArgField))
                                {
                                    filterParameters.Add(new FilterParameter
                                    {
                                        Field = cleanedArgField,
                                        Operator = "CONTAINS",
                                        Value = string.Empty
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(cleanedField))
                        {
                            filterParameters.Add(new FilterParameter
                            {
                                Field = cleanedField,
                                Operator = "CONTAINS",
                                Value = string.Empty
                            });
                        }
                    }
                }
            }
        }





        private void GenerateReport()
        {
            try
            {
                // Собираем только непустые фильтры
                List<FilterParameter> nonEmptyFilters = filterParameters
                    .Where(filter => !string.IsNullOrEmpty(filter.Value))
                    .ToList();

                // Если нет непустых фильтров, выходим
                if (nonEmptyFilters.Count == 0)
                {
                    MessageBox.Show("No filters specified. Please provide at least one filter.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(GetConnectionString()))
                {
                    connection.Open();

                    // Сначала формируем WHERE часть SQL запроса
                    StringBuilder whereClauseBuilder = new StringBuilder("WHERE ");
                    foreach (FilterParameter filter in nonEmptyFilters)
                    {
                        string filterExpression = filter.Operator.Equals("=") ?
                            $"{filter.Field} {filter.Operator} '{filter.Value}'" :
                            $"{filter.Field} LIKE '%{filter.Value}%'";

                        whereClauseBuilder.Append($"{filterExpression} AND ");
                    }

                    whereClauseBuilder.Length -= 5; // Remove the last AND
                    string whereClause = whereClauseBuilder.ToString();
                    string fullQuery = $"{txtSqlQuery.Text} {whereClause};";

                    // Используем полный SQL запрос для получения данных
                    using (MySqlCommand command = new MySqlCommand(fullQuery, connection))
                    {
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                        {
                            DataTable dataTable = new DataTable();
                            // Display row count
                            int rowCount = adapter.Fill(dataTable);
                            rowCountTextBlock.Text = $"Row Count: {rowCount}";

                            // Отображаем результаты в DataGrid
                            dataGrid.ItemsSource = dataTable.DefaultView;
                            dataGrid.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveQueryButton_Click(object sender, RoutedEventArgs e)
        {
            // Сохраняем текущий запрос с запрошенным именем
            string queryName = Microsoft.VisualBasic.Interaction.InputBox("Enter a name for the query:", "Save Query", "");
            if (!string.IsNullOrWhiteSpace(queryName))
            {
                string currentQuery = txtSqlQuery.Text;
                if (!string.IsNullOrWhiteSpace(currentQuery))
                {
                    SavedQuery savedQuery = new SavedQuery { Name = queryName, Query = currentQuery };
                    savedQueries.Add(savedQuery);
                    SaveSavedQueries();  // Сохранение изменений в сохраненных запросах
                }
            }
        }

        private void LoadSavedQueries()
        {
            try
            {
                if (File.Exists("savedQueries.dat"))
                {
                    using (FileStream fs = new FileStream("savedQueries.dat", FileMode.Open))
                    {
                        BinaryFormatter formatter = new BinaryFormatter();

                        // Проверяем, что поток не пустой
                        if (fs.Length > 0)
                        {
                            // Десериализуем сохраненные запросы
                            savedQueries = (ObservableCollection<SavedQuery>)formatter.Deserialize(fs);
                        }
                        else
                        {
                            // Если поток пуст, инициализируем пустым списком
                            savedQueries = new ObservableCollection<SavedQuery>();
                        }
                    }

                    // Обновляем ComboBox с сохраненными запросами
                    queryComboBox.ItemsSource = savedQueries.Select(q => q.Name);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading saved queries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void SaveSavedQueries()
        {
            try
            {
                using (FileStream fs = new FileStream("savedQueries.dat", FileMode.Create))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    formatter.Serialize(fs, savedQueries);
                }

                // Обновляем ComboBox с сохраненными запросами
                queryComboBox.ItemsSource = savedQueries.Select(q => q.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving saved queries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void QueryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (queryComboBox.SelectedItem != null)
            {
                string selectedQueryName = queryComboBox.SelectedItem.ToString();
                SavedQuery selectedQuery = savedQueries.FirstOrDefault(q => q.Name == selectedQueryName);

                if (selectedQuery != null)
                {
                    txtSqlQuery.Text = selectedQuery.Query;
                }
            }

            GenerateFilters();
        }

    }

}

