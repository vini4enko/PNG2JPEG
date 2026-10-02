using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Threading.Tasks;
using ImageMagick;


namespace PngToJpegConverter;
public partial class MainWindow : Window
{
    private string? _selectedFilePath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void SelectFileButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите PNG изображение",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("PNG files") { Patterns = new[] { "*.png" } } }
        });

        if (files.Count > 0)
        {
            // Преобразуем file:// URI в обычный путь
            var uri = new Uri(files[0].Path.AbsolutePath);
            _selectedFilePath = uri.LocalPath; // даст "C:\...\image.png"

            StatusTextBlock.Text = $"Выбран: {System.IO.Path.GetFileName(_selectedFilePath)}";
            ConvertButton.IsEnabled = true;
            await LoadPreviewAsync(_selectedFilePath);
        }
    }

    private async Task LoadPreviewAsync(string path)
    {
        try
        {
            // Используем Task.Run, чтобы не замораживать интерфейс при загрузке больших PNG
            var bitmap = await Task.Run(() =>
            {
                // ВАЖНО: MagickImage должен быть создан и уничтожен внутри этого блока Task.Run
                using var magicImage = new MagickImage(path);
                magicImage.Resize(400, 0);

                return magicImage.ToWriteableBitmap();//.ToAvaloniaBitmap();
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                PreviewImage.Source = bitmap;
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusTextBlock.Text = "Ошибка загрузки превью: " + ex.Message;
            });
        }
    }

    private async void ConvertButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath)) return;

        if (!int.TryParse(QualityTextBox.Text, out int quality) || quality < 1 || quality > 100)
        {
            StatusTextBlock.Text = "Введите корректное значение качества от 1 до 100.";
            return;
        }

        string outputPath = System.IO.Path.ChangeExtension(_selectedFilePath, ".jpg");

        try
        {
            await Task.Run(() =>
            {
                using var image = new MagickImage(_selectedFilePath);
                image.ColorAlpha(MagickColors.White);
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)quality;

                // ИСПРАВЛЕНИЕ 2: В новых версиях нет ColorProfile.Srgb. 
                // Для принудительного перевода в sRGB используется встроенный профиль.
                // Класс ColorProfile имеет конструктор, принимающий ColorSpace.
                //image.AddProfile(new ColorProfile(ColorSpace.sRGB));

                image.Write(outputPath);
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusTextBlock.Text = $"Готово! Файл сохранен: {System.IO.Path.GetFileName(outputPath)}";
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusTextBlock.Text = "Ошибка конвертации: " + ex.Message;
            });
        }
    }
}