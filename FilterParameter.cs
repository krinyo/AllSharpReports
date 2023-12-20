using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllSharpReports
{
    // FilterParameter class
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

}
