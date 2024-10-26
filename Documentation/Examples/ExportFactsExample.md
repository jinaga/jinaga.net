# Exporting Facts in a MAUI Application

This example demonstrates how to use the `ExportFactsToJson` and `ExportFactsToFactual` methods in a MAUI application to export facts and share them using `Share.Default.RequestAsync`.

## Prerequisites

- A MAUI application project
- Jinaga library installed

## Example Code

First, create an extension method to configure the Jinaga client in your application:

```csharp
public static class ServiceCollectionExtensions
{
    public static MauiAppBuilder UseJinagaClient(this MauiAppBuilder builder, Action<JinagaOptions> configureOptions)
    {
        builder.Services.AddSingleton(services =>
        {
            var options = new JinagaOptions();
            configureOptions(options);
            return new JinagaClient(options);
        });

        return builder;
    }
}
```

Then use this extension method in your MauiProgram.cs:

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseJinagaClient(options =>
            {
                // Configure Jinaga options here
            });

        return builder.Build();
    }
}
```

Then use dependency injection in your page:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Jinaga;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Essentials;

namespace MyMauiApp
{
    public partial class MainPage : ContentPage
    {
        private readonly JinagaClient jinagaClient;

        public MainPage(JinagaClient jinagaClient)
        {
            InitializeComponent();
            this.jinagaClient = jinagaClient;
        }

        private async void OnExportFactsToJsonClicked(object sender, EventArgs e)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                // Export facts to the temporary file
                using (var fileStream = File.OpenWrite(tempFile))
                {
                    await jinagaClient.ExportFactsToJson(fileStream);
                }

                // Share the file
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Exported Facts (JSON)",
                    File = new ShareFile(tempFile)
                });

                // Clean up
                File.Delete(tempFile);
            }
            catch
            {
                // Clean up the temporary file if something went wrong
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
                throw;
            }
        }

        private async void OnExportFactsToFactualClicked(object sender, EventArgs e)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                // Export facts to the temporary file
                using (var fileStream = File.OpenWrite(tempFile))
                {
                    await jinagaClient.ExportFactsToFactual(fileStream);
                }

                // Share the file
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Exported Facts (Factual)",
                    File = new ShareFile(tempFile)
                });

                // Clean up
                File.Delete(tempFile);
            }
            catch
            {
                // Clean up the temporary file if something went wrong
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
                throw;
            }
        }
    }
}
```

## Explanation

1. The `UseJinagaClient` extension method in ServiceCollectionExtensions.cs registers a singleton instance of `JinagaClient` with the dependency injection container.
2. The `MainPage` class receives a `JinagaClient` instance through constructor injection.
3. The `OnExportFactsToJsonClicked` method:
   - Creates a temporary file using Path.GetTempFileName()
   - Exports facts to the file in JSON format
   - Shares the file using `Share.Default.RequestAsync`
   - Cleans up the temporary file after sharing
4. The `OnExportFactsToFactualClicked` method:
   - Creates a temporary file using Path.GetTempFileName()
   - Exports facts to the file in Factual format
   - Shares the file using `Share.Default.RequestAsync`
   - Cleans up the temporary file after sharing

## Usage

1. Add buttons to your XAML file and bind their `Clicked` events to the corresponding methods in the `MainPage` class.
2. Run the application and click the buttons to export and share facts.
