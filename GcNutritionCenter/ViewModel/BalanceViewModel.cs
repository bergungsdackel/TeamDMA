﻿using Microsoft.Windows.Themes;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace GcNutritionCenter
{
    // TODO: Logger
    internal class BalanceViewModel : BaseViewModel
    {
        private const string fileName = "data.json";

        private readonly ILogger<BalanceViewModel> _logger;


        private ObservableCollection<Customer> _CustomerList;
        public ObservableCollection<Customer> CustomerList
        {
            get 
            { 
                return _CustomerList; 
            }
            set
            {
                _CustomerList = value;
            }
        }

        private ObservableCollection<Customer> _ItemsSource;
        public ObservableCollection<Customer> ItemsSource
        {
            get
            {
                return _ItemsSource;
            }
            set
            {
                SetProperty(ref _ItemsSource, value);
            }
        }

        public bool IsItemSelected
        {
            get
            {
                return _SelectedCustomer != null;
            }
        }

        private Customer _SelectedCustomer;
        public Customer SelectedCustomer
        {
            get
            {
                return _SelectedCustomer;
            }
            set
            {
                _SelectedCustomer = value;
            }
        }

        private string _searchBoxText;
        public string SearchBoxText
        {
            get
            {
                return _searchBoxText;
            }
            set
            {
                SetProperty(ref _searchBoxText, value);
            }
        }

        public BalanceViewModel(object parent) : base(parent)
        {
            CustomerList = new ObservableCollection<Customer>();
            ObservableCollection<Customer>? tmpList = JsonFile.ReadFromFile<ObservableCollection<Customer>>(fileName);
            if (tmpList != null)
            {
                CustomerList = new ObservableCollection<Customer>(tmpList);
            }

            ItemsSource = CustomerList;
            
            // add events
            foreach(Customer customer in CustomerList)
            {
                customer.PropertyChanged += CustomerChanged;
            }
            CustomerList.CollectionChanged += CustomerListChanged;
        }

        ~BalanceViewModel()
        {
            this.Dispose();
        }

        public void Save()
        {
            // convert in json and save
            JsonFile.SaveToFile(CustomerList, fileName);
        }

        #region Events

        private void CustomerChanged(object? sender, EventArgs e)
        {
            this.Save();
        }

        private void CustomerListChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach(Customer customer in e.NewItems!)
                {
                    customer.PropertyChanged -= CustomerChanged;
                    customer.PropertyChanged += CustomerChanged;
                }
            }
            else if(e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (Customer customer in e.OldItems!)
                {
                    customer.PropertyChanged -= CustomerChanged;
                }
            }

            this.Save();
        }

        #endregion

        #region Commands definitions

        private ICommand _addCustomerCommand;
        public ICommand AddCustomerCommand
        {
            get
            {
                return _addCustomerCommand ?? (_addCustomerCommand = new RelayCommand(param => this.CanAddCustomer(), param => this.AddCustomer()));
            }
        }

        private ICommand _deleteCustomerCommand;
        public ICommand DeleteCustomerCommand
        {
            get
            {
                return _deleteCustomerCommand ?? (_deleteCustomerCommand = new RelayCommand(param => this.CanDeleteCustomer(), param => this.DeleteCustomer()));
            }
        }

        private ICommand _changeCustomerBalanceCommand;
        public ICommand ChangeCustomerBalanceCommand
        {
            get
            {
                return _changeCustomerBalanceCommand ?? (_changeCustomerBalanceCommand = new RelayCommand(param => this.CanChangeCustomerBalance(), param => this.ChangeCustomerBalance(param)));
            }
        }

        private ICommand _keyUpCommand;
        public ICommand KeyUpCommand
        {
            get
            {
                return _keyUpCommand ?? (_keyUpCommand = new RelayCommand(param => this.CanKeyUp(), param => this.KeyUp()));
            }
        }

        private ICommand _deleteSearchTextCommand;
        public ICommand DeleteSearchTextCommand
        {
            get
            {
                return _deleteSearchTextCommand ?? (_deleteSearchTextCommand = new RelayCommand(param => this.CanDeleteSearchText(), param => this.DeleteSearchText()));
            }
        }

        private ICommand _lostFocusCommand;
        public ICommand LostFocusCommand
        {
            get
            {
                return _lostFocusCommand ?? (_lostFocusCommand = new RelayCommand(param => true, param => this.OnLostFocus(param)));
            }
        }

        #endregion

        #region Commands

        // TODO: Bug - if unselectall then "Kunde löschen" is no accessible anymore
        private void OnLostFocus(object param)
        {
            if(param != null && param is DataGrid)
            {
                DataGrid customerGrid = (DataGrid)param;
                customerGrid.UnselectAll();
            }
        }

        private bool CanDeleteSearchText()
        {
            return (SearchBoxText != null && SearchBoxText != String.Empty);
        }
        private void DeleteSearchText()
        {
            SearchBoxText = String.Empty;
            KeyUp();
        }

        private bool CanKeyUp()
        {
            return true;
        }
        private void KeyUp()
        {
            string txtBoxText = SearchBoxText;
            if(txtBoxText != null)
            {
               if(txtBoxText.Length > 0)
                {
                    var filtered = CustomerList.Where(x => ((x.FirstName?.IndexOf(txtBoxText, 0, StringComparison.OrdinalIgnoreCase) != -1) || (x.LastName?.IndexOf(txtBoxText, 0, StringComparison.OrdinalIgnoreCase) != -1) || x.UserID.ToString().Contains(txtBoxText)));
                    ItemsSource = new ObservableCollection<Customer>(filtered);
                }
                else
                {
                    ItemsSource = CustomerList;
                }
            } 
        }

        private bool CanChangeCustomerBalance()
        {
            return true;
        }
        private void ChangeCustomerBalance(object param)
        {
            if(param != null && param is System.Windows.Controls.DataGridCellInfo)
            {
                System.Windows.Controls.DataGridCellInfo cell = (System.Windows.Controls.DataGridCellInfo)param;
                                
                if(cell.Column.Header.Equals("Guthaben")) // TODO: Dynamisch
                {
                    if(cell.Item != null && cell.Item is Customer)
                    {
                        Customer? _curCustomer = cell.Item as Customer;

                        if (IsItemSelected)
                        {
                            ChangeCustomerBalanceDialog inputDialog = new ChangeCustomerBalanceDialog();
                            if (inputDialog.ShowDialog() == true && inputDialog.Answer != null)
                            {
                                decimal addValue = (decimal)inputDialog.Answer;
                                Transaction generatedTransaction = _curCustomer!.ChangeBalance(addValue);
                                MainWindowViewModel? parentVM = ParentViewModel as MainWindowViewModel;
                                if(parentVM != null)
                                {
                                    parentVM.TransactionsViewModel.TransactionList.Add(generatedTransaction);
                                }
                            }
                            else
                            {
                                // canceled
                            }
                        }
                    }
                }
            }
        }

        public bool CanAddCustomer()
        {
            return true;
        }
        public void AddCustomer()
        {
            AddCustomerDialog inputDialog = new AddCustomerDialog();
            if (inputDialog.ShowDialog() == true)
            {
                (string firstname, string lastname) = inputDialog.Answer;
                if(firstname.Length + lastname.Length >= 1)
                {
                    CustomerList.Add(new Customer(firstName: !string.IsNullOrEmpty(firstname) ? firstname : "", lastName: !string.IsNullOrEmpty(lastname) ? lastname : "", collectionToCheckForID: CustomerList));
                }
                else
                {
                    CustomDialog warningDialog = new CustomDialog("Name zu kurz!", CustomDialog.InputType.Ok);
                    warningDialog.ShowDialog();
                }
            }
            else
            {
                // canceled
            }
        }

        public bool CanDeleteCustomer()
        {            
            return (SelectedCustomer != null);
        }
        public void DeleteCustomer()
        {
            try
            {
                if(SelectedCustomer != null)
                {
                    CustomDialog areYouSureDialog = new CustomDialog($"Bist du sicher, dass du den Kunden \"{(SelectedCustomer.FirstName + " " + SelectedCustomer.LastName).Trim()}\" löschen möchtest?", CustomDialog.InputType.YesNo);
                    if(areYouSureDialog.ShowDialog() == true)
                    {
                        CustomDialog deleteTransactionsDialog = new CustomDialog($"Möchtest du auch alle relevanten Transaktionen zu \"{(SelectedCustomer.FirstName + " " + SelectedCustomer.LastName).Trim()}\" löschen?", CustomDialog.InputType.YesNo);
                        if (deleteTransactionsDialog.ShowDialog() == true)
                        {
                            MainWindowViewModel? parentVM = ParentViewModel as MainWindowViewModel;
                            if(parentVM != null)
                            {
                                foreach(Transaction transaction in parentVM.TransactionsViewModel.TransactionList.ToArray())
                                {
                                    if(transaction.Customer!.UserID == SelectedCustomer.UserID)
                                    {
                                        parentVM.TransactionsViewModel.TransactionList.Remove(transaction);
                                    }
                                }
                            }
                        }
                        CustomerList.Remove(SelectedCustomer);
                    }          
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        #endregion

        public override void Dispose()
        {
            this.Save();


            // call from base
            base.Dispose();
        }
    }
}
