using System.Windows.Shapes;
using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SP26InventoryManagement.Repositories
{
    public class AdjustmentRepository : IAdjustmentRepository
    {
        private readonly Sp26inventoryManagementDbContext _context;

        public AdjustmentRepository(Sp26inventoryManagementDbContext context)
        {
            _context =  context;
        }

        public IEnumerable<Product> GetProducts()  => _context.Products.ToList();
        public IEnumerable<Warehouse> GetWarehouses()  => _context.Warehouses.ToList();
        public IEnumerable<ProductLot> GetProductLots()  => _context.ProductLots.ToList();

        // 1. Lấy danh sách phiếu ở trạng thái 'DRAFT'
        public IEnumerable<StockTransaction> GetUnpostedTransactions()
        {
            return _context.StockTransactions
                .Where(t =>  t.DocumentStatus ==  "DRAFT")
                .OrderByDescending(t =>  t.CreatedAt)
                .ToList();
        }

        // 2. Tạo phiếu mới với trạng thái 'DRAFT'
        public void CreateAdjustment(StockTransaction header,  List < StockTransactionLine >  lines)
        {
            using var transaction =  _context.Database.BeginTransaction();
            try
            {
                header.DocumentStatus =  "DRAFT"; // Khớp với Constraint [DocumentStatus]='DRAFT'
                _context.StockTransactions.Add(header);
                _context.SaveChanges();

                foreach(var line in lines)
                {
                    line.TransactionId =  header.TransactionId;
                    _context.StockTransactionLines.Add(line);
                }
                _context.SaveChanges();
                transaction.Commit();
            }
            catch(Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        // 3. Post phiếu và chuyển sang trạng thái 'POSTED'
        public void PostAdjustment(long transactionId,  int userId)
        {
            using var transaction =  _context.Database.BeginTransaction();
            try
            {
                var header =  _context.StockTransactions
                    .Include(t =>  t.StockTransactionLines)
                    .FirstOrDefault(t =>  t.TransactionId ==  transactionId);

                if(header ==  null ||  header.DocumentStatus !=  "DRAFT")
                    throw new Exception("Phiếu không hợp lệ hoặc đã được xác nhận.");

                foreach(var line in header.StockTransactionLines)
                {
                    var balance =  _context.StockBalances.FirstOrDefault(b => 
                        b.WarehouseId ==  header.WarehouseId && 
                        b.ProductId ==  line.ProductId && 
                        b.ProductLotId ==  line.ProductLotId);

                    if(balance !=  null)
                    {
                        balance.OnHandQty +=  line.Qty;
                        balance.UpdatedAt =  DateTime.Now;
                        balance.LastMovementAt =  DateTime.Now;
                    }
                    else
                    {
                        _context.StockBalances.Add(new StockBalance
                        {
                            // Fix lỗi CS0019: Xóa '?? 0' vì WarehouseId không nullable
                            WarehouseId =  header.WarehouseId,
                            ProductId =  line.ProductId,
                            ProductLotId =  line.ProductLotId,
                            OnHandQty =  line.Qty,
                            UpdatedAt =  DateTime.Now,
                            LastMovementAt =  DateTime.Now
                        });
                    }
                }

                header.DocumentStatus =  "POSTED"; // Khớp với Constraint [DocumentStatus]='POSTED'
                header.PostedByUserId =  userId;
                header.PostedAt =  DateTime.Now;
                header.UpdatedAt =  DateTime.Now;

                _context.SaveChanges();
                transaction.Commit();
            }
            catch(Exception)
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}