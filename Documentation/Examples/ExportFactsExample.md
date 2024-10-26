# Exporting Facts in a MAUI Application

This example demonstrates how to use the `ExportFactsToJson` and `ExportFactsToFactual` methods in a MAUI application to export facts and share them using `Share.Default.RequestAsync`.

## Prerequisites

- A MAUI application project
- Jinaga library installed

## Example Code

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

        public MainPage()
        {
            InitializeComponent();
            jinagaClient = new JinagaClient(new JinagaOptions
            {
                // Configure Jinaga options here
            });
        }

        private async void OnExportFactsToJsonClicked(object sender, EventArgs e)
        {
            using (var memoryStream = new MemoryStream())
            {
                await jinagaClient.ExportFactsToJson(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                var json = new StreamReader(memoryStream).ReadToEnd();

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = json,
                    Title = "Exported Facts (JSON)"
                });
            }
        }

        private async void OnExportFactsToFactualClicked(object sender, EventArgs e)
        {
            using (var memoryStream = new MemoryStream())
            {
                await jinagaClient.ExportFactsToFactual(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                var factual = new StreamReader(memoryStream).ReadToEnd();

                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = factual,
                    Title = "Exported Facts (Factual)"
                });
            }
        }
    }
}
```

## Explanation

1. The `MainPage` class initializes a `JinagaClient` instance with the necessary options.
2. The `OnExportFactsToJsonClicked` method exports facts to JSON format and shares them using `Share.Default.RequestAsync`.
3. The `OnExportFactsToFactualClicked` method exports facts to Factual format and shares them using `Share.Default.RequestAsync`.

## Usage

1. Add buttons to your XAML file and bind their `Clicked` events to the corresponding methods in the `MainPage` class.
2. Run the application and click the buttons to export and share facts.
