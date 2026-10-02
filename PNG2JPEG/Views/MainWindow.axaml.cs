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
            var uri = new Uri(path);
            var fileRef = await TopLevel.GetTopLevel(this)!.StorageProvider.TryGetFileFromPathAsync(uri);

            using var stream = await fileRef.OpenReadAsync();

            var bitmap = await Task.Run(() =>
            {
                using var magicImage = new MagickImage(stream);
                magicImage.Resize(400, 0);
                return magicImage.ToWriteableBitmap();
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


    /*
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
    }*/

    private async void ConvertButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath)) return;

        if (!int.TryParse(QualityTextBox.Text, out int quality) || quality < 1 || quality > 100)
        {
            StatusTextBlock.Text = "Введите корректное значение качества от 1 до 100.";
            return;
        }

        // Путь для выходного файла можно оставить обычным (он будет в той же папке, что и исходный)
        string outputPath = System.IO.Path.ChangeExtension(_selectedFilePath, ".jpg");

        try
        {
            // 1. Получаем FileReference по URI пути (который мы уже сохранили как LocalPath)
            var uri = new Uri(_selectedFilePath);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                StatusTextBlock.Text = "Не удалось получить окно.";
                return;
            }

            var fileRef = await topLevel.StorageProvider.TryGetFileFromPathAsync(uri);

            // 2. Открываем поток на чтение
            using var readStream = await fileRef.OpenReadAsync();

            // 3. Конвертируем в памяти (без промежуточного сохранения)
            await Task.Run(() =>
            {
                using var image = new MagickImage(readStream);
                image.ColorAlpha(MagickColors.White);
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)quality;

                // 4. Записываем в выходной файл (тут уже можно по пути, т.к. мы сами создаём файл)
                image.Write(outputPath);
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusTextBlock.Text = $"Готово! Файл сохранён: {System.IO.Path.GetFileName(outputPath)}";
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

    /*
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
    }*/
}