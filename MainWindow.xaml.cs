using System.Collections.ObjectModel;
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
        #region Fields and Properties

        private ObservableCollection<FilterParameter> filterParameters;
        private ObservableCollection<SavedQuery> savedQueries = new ObservableCollection<SavedQuery>();

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            InitializeData();
        }

        private void InitializeData()
        {
            filterParameters = new ObservableCollection<FilterParameter>();
            filterFieldsPanel.DataContext = filterParameters;

            LoadCredentials();
            LoadSavedQueries();  // Load saved queries
        }

        #endregion

        #region Event Handlers

        private void SaveCredentialsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCredentials(CreateCredentialsFromUI());
        }

        private void CheckQueryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckSqlQuery();
                GenerateFilters();
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
                GenerateReport();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveQueryButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentQuery();
        }

        private void QueryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadSelectedQuery();
        }

        #endregion

        #region Save and Load Credentials

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

                        SetCredentialsUI(credentials);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region SQL Query and Filters
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
                            rowCountTextBlock.Text = $"{rowCount}";

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
        private void CheckSqlQuery()
        {
            using (MySqlConnection connection = new MySqlConnection(GetConnectionString()))
            {
                connection.Open();
                using (MySqlCommand command = new MySqlCommand(txtSqlQuery.Text, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void GenerateFilters()
        {
            filterParameters.Clear();

            string selectFields = ExtractSelectFieldsFromQuery(txtSqlQuery.Text);
            string[] fields = SplitFields(selectFields);

            foreach (string field in fields)
            {
                // Process each field
                ProcessField(field);
            }
        }

        private string ExtractSelectFieldsFromQuery(string query)
        {
            string pattern = @"SELECT(.+?)FROM";
            Match match = Regex.Match(query, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private string[] SplitFields(string selectFields)
        {
            // Split fields considering ',' inside functions
            return Regex.Split(selectFields, @",(?![^\(]*\))");
        }

        private void ProcessField(string field)
        {
            string cleanedField = Regex.Replace(field, @"\s+AS\s+\S+$", string.Empty).Trim();

            if (cleanedField.Contains("CONCAT"))
            {
                ProcessConcatField(cleanedField);
            }
            else
            {
                AddFilterParameter(cleanedField);
            }
        }

        private void ProcessConcatField(string concatField)
        {
            MatchCollection concatArgs = Regex.Matches(concatField, @"CONCAT\(([^)]+)\)");

            foreach (Match concatArg in concatArgs)
            {
                string[] argFields = concatArg.Groups[1].Value.Split(',');

                foreach (string argField in argFields)
                {
                    string cleanedArgField = argField.Trim();
                    if (!string.IsNullOrEmpty(cleanedArgField))
                    {
                        AddFilterParameter(cleanedArgField);
                    }
                }
            }
        }

        private void AddFilterParameter(string field)
        {
            filterParameters.Add(new FilterParameter
            {
                Field = field,
                Operator = "CONTAINS",
                Value = string.Empty
            });
        }

        #endregion

        #region Save and Load Queries

        private void SaveCurrentQuery()
        {
            string queryName = Microsoft.VisualBasic.Interaction.InputBox("Enter a name for the query:", "Save Query", "");

            if (!string.IsNullOrWhiteSpace(queryName))
            {
                string currentQuery = txtSqlQuery.Text;

                if (!string.IsNullOrWhiteSpace(currentQuery))
                {
                    SavedQuery savedQuery = new SavedQuery { Name = queryName, Query = currentQuery };
                    savedQueries.Add(savedQuery);
                    SaveSavedQueries();
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

                        if (fs.Length > 0)
                        {
                            savedQueries = (ObservableCollection<SavedQuery>)formatter.Deserialize(fs);
                        }
                        else
                        {
                            savedQueries = new ObservableCollection<SavedQuery>();
                        }
                    }

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

                queryComboBox.ItemsSource = savedQueries.Select(q => q.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving saved queries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSelectedQuery()
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

        #endregion

        #region Helpers

        private Credentials CreateCredentialsFromUI()
        {
            return new Credentials
            {
                ServerAddress = txtServerAddress.Text,
                Username = txtUsername.Text,
                Password = txtPassword.Password,
                DatabaseName = txtDatabaseName.Text
            };
        }

        private void SetCredentialsUI(Credentials credentials)
        {
            txtServerAddress.Text = credentials.ServerAddress;
            txtUsername.Text = credentials.Username;
            txtPassword.Password = credentials.Password;
            txtDatabaseName.Text = credentials.DatabaseName;
        }

        private string GetConnectionString()
        {
            return $"Server={txtServerAddress.Text};Database={txtDatabaseName.Text};User ID={txtUsername.Text};Password={txtPassword.Password};CharSet=utf8mb3;";
        }

        #endregion
    }
}
