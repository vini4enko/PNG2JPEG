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
            _selectedFilePath = uri.LocalPath;

            StatusTextBlock.Text = $"Выбран: {System.IO.Path.GetFileName(_selectedFilePath)}";
            ConvertButton.IsEnabled = true;
            await LoadPreviewAsync(_selectedFilePath);
        }
    }

    private async Task LoadPreviewAsync(string path)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            // Получаем файл по пути (новый API)
            var uri = new Uri(path);
            var fileRef = await topLevel.StorageProvider.TryGetFileFromPathAsync(uri);
            if (fileRef == null)
            {
                StatusTextBlock.Text = "Не удалось получить доступ к файлу.";
                return;
            }

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

    private async void ConvertButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFilePath)) return;

        if (!int.TryParse(QualityTextBox.Text, out int quality) || quality < 1 || quality > 100)
        {
            StatusTextBlock.Text = "Введите корректное значение качества от 1 до 100.";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        try
        {
            // --- 1. Чтение исходного файла через поток ---

            var inputUri = new Uri(_selectedFilePath);
            var inputFile = await topLevel.StorageProvider.TryGetFileFromPathAsync(inputUri);
            if (inputFile == null)
            {
                StatusTextBlock.Text = "Не удалось получить доступ к исходному файлу.";
                return;
            }

            // --- 2. Диалог выбора места сохранения ---

            var defaultName = System.IO.Path.ChangeExtension(
                    System.IO.Path.GetFileName(_selectedFilePath), ".jpg");

            var saveFile = await topLevel.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Сохранить как JPEG",
                    SuggestedFileName = defaultName,
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("JPEG files") { Patterns = new[] { "*.jpg" } }
                    }
                });


            if (saveFile == null)
            {
                StatusTextBlock.Text = "Сохранение отменено.";
                return;
            }

            StatusTextBlock.Text = "Конвертация...";

            // --- 3. Конвертация: читаем поток, пишем поток ---

            using var readStream = await inputFile.OpenReadAsync();
            using var writeStream = await saveFile.OpenWriteAsync();

            await Task.Run(() =>
            {
                using var image = new MagickImage(readStream);
                image.ColorAlpha(MagickColors.White);
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)quality;
                image.Write(writeStream);
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusTextBlock.Text = $"Готово! Сохранено: {saveFile.Name}";
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
