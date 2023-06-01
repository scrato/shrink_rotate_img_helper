using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.Win32;
using ImageMagick;
using Ookii.Dialogs;
using System.Windows.Input;
using System.Threading.Tasks;

namespace shrink_rotate_img_helper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string selectedImagePath;
        private BitmapImage _currentImage;
        private Rotation _currentRotation = Rotation.Rotate0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();

            if (dialog.ShowDialog() == true)
            {
                PopulateTreeView(dialog.SelectedPath);
            }
        }

        private void PopulateTreeView(string folderPath)
        {
            var directories = Directory.GetDirectories(folderPath);
            foreach (var directory in directories)
            {
                var item = new TreeViewItem
                {
                    Header = Path.GetFileName(directory),
                    Tag = directory
                };

                var imageFiles = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Where(s => s.EndsWith(".jpeg") || s.EndsWith(".jpg") || s.EndsWith(".png"));

                foreach (var imageFile in imageFiles)
                {
                    var subItem = new TreeViewItem
                    {
                        Header = Path.GetFileName(imageFile),
                        Tag = imageFile
                    };
                    item.Items.Add(subItem);
                }

                treeView.Items.Add(item);
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (treeView.SelectedItem is not TreeViewItem treeViewItem) return;

            selectedImagePath = treeViewItem.Tag.ToString();

            if (!File.Exists(selectedImagePath))
                return;

            FileAttributes attr = File.GetAttributes(selectedImagePath);

            if (attr.HasFlag(FileAttributes.Directory))
                return;
            _currentRotation = Rotation.Rotate0;
            RefreshBitmap();

            
        } 
        
            
        
        private void RefreshBitmap()
        {
            var bitmap = new BitmapImage();
            using (FileStream fs = new FileStream(selectedImagePath, FileMode.Open))
            {
                bitmap.BeginInit();
                bitmap.StreamSource = fs;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.Rotation = _currentRotation;
                bitmap.EndInit();
                bitmap.Freeze();
            }

            imageView.Source = bitmap;
            _currentImage = bitmap;
        }

        private void RotateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImage != null)
            {
                _currentRotation = (Rotation)((int) (_currentRotation + 1) % 4);
                RefreshBitmap();
            }
        }

        

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            using (var image = new MagickImage(selectedImagePath))
            {
                switch (_currentRotation)
                {
                    case Rotation.Rotate0:
                        break;
                    case Rotation.Rotate90:
                        image.Rotate(90);
                        break;
                    case Rotation.Rotate180:
                        image.Rotate(180);
                        break;
                    case Rotation.Rotate270:
                        image.Rotate(270);
                        break;
                    default:
                        break;
                }
                //var filePath = Path.Combine(Path.GetDirectoryName(selectedImagePath)
                //    ,$"{Path.GetFileNameWithoutExtension(selectedImagePath)}.shrinked{Path.GetExtension(selectedImagePath)}");
                image.Write(selectedImagePath);
                
            }
        }

        private void ShrinkButton_Click(object sender, RoutedEventArgs e)
        {
            var inputBox = new InputBoxWindow("Enter maximum image size in megabytes");
            if (inputBox.ShowDialog() == true)
            {
                var maxSize = int.Parse(inputBox.InputText) * 1024 * 1024;
                foreach (TreeViewItem item in treeView.Items)
                {
                    var arr = new TreeViewItem[item.Items.Count];
                    item.Items.CopyTo(arr,0);
                    var imagePaths = arr.Select((subItem) => Dispatcher.Invoke(() => subItem.Tag.ToString())).ToList();

                    Parallel.ForEach(imagePaths, (imagePath) => {
                        using (var image = new MagickImage(imagePath))
                        {
                            var hasDone = false;
                            while (image.ToByteArray().Length > maxSize)
                            {
                                hasDone = true;
                                var settings = new MagickGeometry(new Percentage(90), new Percentage(90))
                                {
                                    IgnoreAspectRatio = false
                                };
                                image.Resize(settings);
                            }

                            if (hasDone)
                                image.Write(imagePath);
                        }
                    });
                    
                }
                MessageBox.Show("Finished!");
            }
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs e)
        {
            var treeView = (TreeView)sender;
            var selectedItem = (TreeViewItem)treeView.SelectedItem;

            if (selectedItem == null) return;

            TreeViewItem nextItem = null;

            switch (e.Key)
            {
                case Key.PageUp:
                    nextItem = GetPreviousItem(selectedItem);
                    break;
                case Key.PageDown:
                    nextItem = GetNextItem(selectedItem);
                    break;
            }

            if (nextItem != null)
            {
                nextItem.IsSelected = true;
                nextItem.Focus();
                e.Handled = true;
            }
        }

        private TreeViewItem GetPreviousItem(TreeViewItem item)
        {
            TreeViewItem previousItem = GetPreviousItem(item);
            if (previousItem != null)
            {
                if (previousItem.HasItems && previousItem.IsExpanded)
                {
                    return previousItem.Items[previousItem.Items.Count - 1] as TreeViewItem;
                }
                return previousItem;
            }

            var parent = item.Parent as TreeViewItem;
            return parent;
        }

        private TreeViewItem GetNextItem(TreeViewItem item)
        {
            if (item.HasItems && item.IsExpanded)
            {
                return item.Items[0] as TreeViewItem;
            }

            TreeViewItem nextItem = GetNextSibling(item);
            if (nextItem != null)
            {
                return nextItem;
            }

            var parent = item.Parent as TreeViewItem;
            while (parent != null)
            {
                nextItem = GetNextSibling(parent);
                if (nextItem != null)
                {
                    return nextItem;
                }

                parent = parent.Parent as TreeViewItem;
            }

            return null;
        }

        TreeViewItem GetNextSibling(TreeViewItem item)
        {
            var parent = item.Parent as ItemsControl;
            if (parent != null)
            {
                var index = parent.ItemContainerGenerator.IndexFromContainer(item);
                if (index < parent.Items.Count - 1)
                {
                    return parent.ItemContainerGenerator.ContainerFromIndex(index + 1) as TreeViewItem;
                }
            }
            return null;
        }

        TreeViewItem GetPrevSibling(TreeViewItem item)
        {
            var parent = item.Parent as ItemsControl;
            if (parent != null)
            {
                var index = parent.ItemContainerGenerator.IndexFromContainer(item);
                if (index > 0)
                {
                    return parent.ItemContainerGenerator.ContainerFromIndex(index - 1) as TreeViewItem;
                }
            }
            return null;
        }


    }
}
