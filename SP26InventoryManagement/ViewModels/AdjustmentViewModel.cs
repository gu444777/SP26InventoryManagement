using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels
{
    public class AdjustmentViewModel : ViewModelBase
    {
        private readonly IAdjustmentRepository _repo;
        private readonly CurrentUserContext _currentUserContext;
        private readonly IMessageService _messageService;

        public ObservableCollection<Product> Products { get; set; } =  new();
        public ObservableCollection<Warehouse> Warehouses { get; set; } =  new();
        public ObservableCollection<ProductLot> ProductLots { get; set; } =  new();

        // MỚI: Danh sách hiển thị các phiếu chưa Post để người dùng chọn
        public ObservableCollection<StockTransaction> UnpostedTransactions { get; set; } =  new();

        public List<string>  TransactionTypes { get; } =  new()
        {
            "ADJUSTMENT_IN", "ADJUSTMENT_OUT", "RECEIPT", "ISSUE", "TRANSFER_IN", "TRANSFER_OUT"
        };

        private string _selectedType =  "ADJUSTMENT_IN";
        public string SelectedType
        {
            get =>  _selectedType;
            set { _selectedType =  value; OnPropertyChanged();  UpdateCommand(); }
        }

        private int? _selectedProductId;
        public int? SelectedProductId
        {
            get =>  _selectedProductId;
            set
            {
                _selectedProductId =  value;
                OnPropertyChanged();
                LoadProductLots(value, SelectedWarehouseId);
                UpdateCommand();
            }
        }

        private int _selectedWarehouseId;
        public int SelectedWarehouseId
        {
            get =>  _selectedWarehouseId;
            set
            {
                _selectedWarehouseId =  value;
                if(TransactionHeader !=  null) TransactionHeader.WarehouseId =  value;
                OnPropertyChanged();
                LoadProductLots(SelectedProductId, value);
                UpdateCommand();
            }
        }

        private ProductLot _selectedLot;
        public ProductLot SelectedLot
        {
            get =>  _selectedLot;
            set { _selectedLot =  value; OnPropertyChanged();  UpdateCommand(); }
        }

        private decimal _inputQty;
        public decimal InputQty
        {
            get =>  _inputQty;
            set { _inputQty =  value; OnPropertyChanged();  UpdateCommand(); }
        }

        private string _reasonInput;
        public string ReasonInput
        {
            get =>  _reasonInput;
            set { _reasonInput =  value; OnPropertyChanged();  UpdateCommand(); }
        }

        private StockTransaction _transactionHeader;
        public StockTransaction TransactionHeader
        {
            get =>  _transactionHeader;
            set { _transactionHeader =  value; OnPropertyChanged(); }
        }

        public ICommand SubmitCommand { get; }
        public ICommand PostCommand { get; }

        public AdjustmentViewModel(IAdjustmentRepository repo,  CurrentUserContext currentUserContext,  IMessageService messageService)
        {
            _repo =  repo;
            _currentUserContext =  currentUserContext;
            _messageService =  messageService;

            SubmitCommand =  new RelayCommand(ExecuteSubmit,  CanSubmit);
            PostCommand =  new RelayCommand<StockTransaction>(ExecutePost); // Đổi tham số sang object để dễ xử lý

            LoadInitialData();
            ResetForm();
        }

        private void LoadInitialData()
        {
            Products.Clear();
            foreach(var p in _repo.GetProducts()) Products.Add(p);

            Warehouses.Clear();
            foreach(var w in _repo.GetWarehouses()) Warehouses.Add(w);

            // Tải danh sách các phiếu đang OPEN để hiển thị lên bảng
            RefreshUnpostedList();
        }

        private void RefreshUnpostedList()
        {
            UnpostedTransactions.Clear();
            var unposted = _repo.GetUnpostedTransactions();
            foreach (var trans in unposted)
            {
                UnpostedTransactions.Add(trans);
            }
        }

        private void LoadProductLots(int ? productId,  int warehouseId)
        {
            ProductLots.Clear();
            if(productId.HasValue &&  warehouseId >  0)
            {
                var lots =  _repo.GetProductLots()
                    .Where(l =>  l.ProductId ==  productId.Value &&  l.WarehouseId ==  warehouseId);

                foreach(var lot in lots) ProductLots.Add(lot);
            }
        }

        private void ResetForm()
        {
            TransactionHeader =  new StockTransaction
            {
                TransactionType =  "ADJUSTMENT_IN",
                DocumentStatus =  "OPEN",
                TransactionDate =  DateTime.Now,
                TransactionNo =  $"ADJ-{DateTime.Now:yyyyMMddHHmmss}",
                CreatedByUserId =  _currentUserContext.UserId ??  1
            }
            ;

            SelectedProductId =  null;
            SelectedWarehouseId =  0;
            SelectedLot =  null;
            InputQty =  0;
            ReasonInput =  string.Empty;
            UpdateCommand();
        }

        private bool CanSubmit()
        {
            return TransactionHeader !=  null &&
                   SelectedWarehouseId >  0 &&
                   SelectedProductId >  0 &&
                   SelectedLot !=  null &&
                   InputQty >  0 &&
                   !string.IsNullOrWhiteSpace(ReasonInput);
        }

        private void UpdateCommand()
        {
            (SubmitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void ExecuteSubmit()
        {
            try
            {
                TransactionHeader.TransactionType =  SelectedType;
                TransactionHeader.AdjustmentReason =  ReasonInput;
                TransactionHeader.Remarks =  ReasonInput; // Đồng bộ luôn cột Remarks
                TransactionHeader.ReferenceType =  "STOCKTAKE";
                TransactionHeader.ReferenceNo =  TransactionHeader.TransactionNo;
                TransactionHeader.CreatedAt =  DateTime.Now;

                decimal finalQty =  SelectedType.Contains("OUT") ||  SelectedType ==  "ISSUE"
                    ?  InputQty *  - 1
                    : InputQty;

                var line =  new StockTransactionLine
                {
                    ProductId =  SelectedProductId.Value,
                    ProductLotId =  SelectedLot.ProductLotId,
                    Qty =  finalQty,
                    UnitCost =  SelectedLot.UnitCost,
                    CreatedAt =  DateTime.Now
                }
                ;

                _repo.CreateAdjustment(TransactionHeader,  new List < StockTransactionLine >  {  line });

                _messageService.ShowInfo("Draft Adjustment Saved! Please 'Post' to update inventory.");
                ResetForm();
                RefreshUnpostedList(); // Cập nhật lại danh sách chờ sau khi lưu
            }
            catch(Exception ex)
            {
                var msg =  ex.InnerException !=  null?  ex.InnerException.Message : ex.Message;
                _messageService.ShowError($"Database Error: {msg}");
            }
        }

        private void ExecutePost(StockTransaction trans)
        {
            if(trans ==  null) return;
            try
            {
                _repo.PostAdjustment(trans.TransactionId,  _currentUserContext.UserId ??  1);
                _messageService.ShowInfo($"Transaction {trans.TransactionNo} posted successfully!");
                RefreshUnpostedList(); // Xóa khỏi danh sách chờ sau khi thành công
            }
            catch(Exception ex)
            {
                _messageService.ShowError($"Post Error: {ex.Message}");
            }
        }
    }
}