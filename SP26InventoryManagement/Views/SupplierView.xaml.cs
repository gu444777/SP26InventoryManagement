using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement.Views
{
    public partial class SupplierView : Window
    {
        public SupplierView(SupplierViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }

}
