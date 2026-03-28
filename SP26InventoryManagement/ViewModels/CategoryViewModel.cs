using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Infrastructure;

namespace SP26InventoryManagement.ViewModels
{
    public class CategoryViewModel : ViewModelBase
    {
        private readonly ICategoryRepository _repo;
        public ObservableCollection<Category> Categories { get; set; } = new ObservableCollection<Category>();

        private Category _selectedCategory = new Category();
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (value != null && value.CategoryId > 0)
                {
                    _selectedCategory = new Category
                    {
                        CategoryId = value.CategoryId,
                        CategoryCode = value.CategoryCode,
                        CategoryName = value.CategoryName,
                        ParentCategoryId = value.ParentCategoryId,
                        IsActive = value.IsActive,
                        RowVersion = value.RowVersion
                    };
                }
                else
                {
                    _selectedCategory = new Category { CategoryId = 0, IsActive = true, CategoryCode = "" };
                }
                OnPropertyChanged();
                RefreshButtons();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }

        public CategoryViewModel(ICategoryRepository repo)
        {
            _repo = repo;

            SaveCommand = new RelayCommand(ExecuteSave, () =>
                SelectedCategory != null &&
                !string.IsNullOrWhiteSpace(SelectedCategory.CategoryName) &&
                !string.IsNullOrWhiteSpace(SelectedCategory.CategoryCode));

            DeleteCommand = new RelayCommand(ExecuteDelete, () => SelectedCategory?.CategoryId > 0);
            RefreshCommand = new RelayCommand(LoadData);

            LoadData();
        }

        public void RefreshButtons()
        {
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void LoadData()
        {
            var data = _repo.GetAll();
            Categories.Clear();
            foreach (var item in data)
            {
                Categories.Add(item);
            }
            SelectedCategory = new Category { CategoryId = 0, IsActive = true, CategoryCode = "" };
        }

        private void ExecuteSave()
        {
            try
            {
                if (SelectedCategory.CategoryId == 0)
                {
                    SelectedCategory.CreatedAt = DateTime.Now;
                    _repo.Add(SelectedCategory);
                }
                else
                {
                    _repo.Update(SelectedCategory);
                }
                _repo.Save();
                MessageBox.Show("Saved successfully!");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void ExecuteDelete()
        {
            var result = MessageBox.Show("Are you sure you want to delete this category?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _repo.Delete(SelectedCategory.CategoryId);
                    _repo.Save();
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Delete failed! This category might be in use.\nDetails: {ex.Message}");
                }
            }
        }
    }
}