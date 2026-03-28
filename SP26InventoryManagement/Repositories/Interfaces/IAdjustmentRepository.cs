using System.Collections.Generic;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories
{
    public interface IAdjustmentRepository
    {
        IEnumerable<Product>  GetProducts();
        IEnumerable<Warehouse>  GetWarehouses();
        IEnumerable<ProductLot>  GetProductLots();
        // Hàm lấy danh sách phiếu trạng thái chờ
        IEnumerable<StockTransaction>  GetUnpostedTransactions();

        void CreateAdjustment(StockTransaction header,  List< StockTransactionLine >  lines);
        void PostAdjustment(long transactionId,  int userId);
    }
}