using System.Collections.Generic;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories
{
    public interface ICategoryRepository
{
    // Lấy toàn bộ danh sách danh mục (bao gồm cả thông tin danh mục cha)
    List<Category> GetAll();

    // Tìm một danh mục theo ID
    Category? GetById(int id);

    // Thêm mới
    void Add(Category category);

    // Cập nhật thông tin
    void Update(Category category);

    // Xóa
    void Delete(int id);

    // Lưu thay đổi xuống Database
    void Save();
}
}