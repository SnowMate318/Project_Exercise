
using Project_Exercise.Models;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;

namespace Project_Exercise.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _userName;
        private long _age;
        private string _findUserName;

        private readonly UserRepository _repository = new UserRepository(); // Model 의존

        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                OnPropertyChanged(nameof(UserName));
            }
        }
        public long Age
        {
            get => _age;
            set
            {
                _age = value;
                OnPropertyChanged(nameof(Age));
            }
        }
        public string FindUserName
        {
            get => _findUserName;
            set
            {
                _findUserName = value;
                OnPropertyChanged(nameof(FindUserName));
            }
        }


        public ICommand ConfirmCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }

        public MainViewModel()
        {
            ConfirmCommand = new RelayCommand(o =>
            {
                MessageBox.Show($"입력된 이름: {UserName}, 입력된 나이: {Age}");
            });

            SaveCommand = new RelayCommand(o =>
            {

                if (string.IsNullOrWhiteSpace(UserName))
                {
                    MessageBox.Show("사용자 이름을 입력하세요.");
                    return;
                }

                var user = new User { Name = UserName, Age = Age };
                _repository.Save(user);
                MessageBox.Show($"저장 완료: {user.Name}, {user.Age}세");
            });

            LoadCommand = new RelayCommand(o =>
            {
                if (string.IsNullOrWhiteSpace(FindUserName))
                {
                    MessageBox.Show("사용자 이름을 입력하세요.");
                    return;
                }

                var user = _repository.Get(FindUserName);

                if (user != null)
                {
                    Age = user.Age; // UI에 반영됨
                    MessageBox.Show($"조회 성공: {user.Name}, {user.Age}세");
                    return;
                }

                MessageBox.Show($"[{FindUserName}] 사용자 없음");
            });

        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);
    }
}
