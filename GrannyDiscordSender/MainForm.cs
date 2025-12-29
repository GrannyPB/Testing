using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GrannyDiscordSender;

public sealed class MainForm : Form
{
    private readonly TextBox _webhookTextBox = new();
    private readonly TextBox _storyTextBox = new();
    private readonly Label _imageLabel = new();
    private readonly Button _chooseImageButton = new();
    private readonly Button _sendButton = new();
    private readonly Label _statusLabel = new();
    private string? _imagePath;

    public MainForm()
    {
        Text = "Granny's Discord Sender";
        MinimumSize = new Size(640, 520);
        StartPosition = FormStartPosition.CenterScreen;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 7,
            AutoSize = true,
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));

        var header = new Label
        {
            Text = "Send photos and stories to Granny's Discord",
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
        };
        mainPanel.Controls.Add(header, 0, 0);
        mainPanel.SetColumnSpan(header, 2);

        mainPanel.Controls.Add(new Label
        {
            Text = "Discord Webhook URL",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        }, 0, 1);

        _webhookTextBox.Dock = DockStyle.Fill;
        _webhookTextBox.PlaceholderText = "https://discord.com/api/webhooks/...";
        mainPanel.Controls.Add(_webhookTextBox, 1, 1);

        mainPanel.Controls.Add(new Label
        {
            Text = "Story",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        }, 0, 2);

        _storyTextBox.Dock = DockStyle.Fill;
        _storyTextBox.Multiline = true;
        _storyTextBox.Height = 180;
        _storyTextBox.ScrollBars = ScrollBars.Vertical;
        mainPanel.Controls.Add(_storyTextBox, 1, 2);

        mainPanel.Controls.Add(new Label
        {
            Text = "Image",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        }, 0, 3);

        var imagePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
        };

        _chooseImageButton.Text = "Choose Image";
        _chooseImageButton.AutoSize = true;
        _chooseImageButton.Click += ChooseImageClicked;

        _imageLabel.Text = "No image selected";
        _imageLabel.AutoSize = true;
        _imageLabel.Padding = new Padding(8, 8, 0, 0);

        imagePanel.Controls.Add(_chooseImageButton);
        imagePanel.Controls.Add(_imageLabel);
        mainPanel.Controls.Add(imagePanel, 1, 3);

        _sendButton.Text = "Send to Discord";
        _sendButton.AutoSize = true;
        _sendButton.Anchor = AnchorStyles.Left;
        _sendButton.Click += async (_, _) => await SendAsync();
        mainPanel.Controls.Add(_sendButton, 1, 4);

        _statusLabel.Text = "Ready.";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.DimGray;
        mainPanel.Controls.Add(_statusLabel, 1, 5);

        Controls.Add(mainPanel);
    }

    private void ChooseImageClicked(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp",
            Title = "Select an image to share",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _imagePath = dialog.FileName;
            _imageLabel.Text = Path.GetFileName(_imagePath);
        }
    }

    private async Task SendAsync()
    {
        var webhookUrl = _webhookTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            SetStatus("Please enter a Discord webhook URL.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_storyTextBox.Text) && string.IsNullOrWhiteSpace(_imagePath))
        {
            SetStatus("Add a story, an image, or both before sending.", isError: true);
            return;
        }

        ToggleSending(true);
        SetStatus("Sending...", isError: false);

        try
        {
            var story = _storyTextBox.Text.Trim();
            var hasStory = !string.IsNullOrWhiteSpace(story);
            var hasImage = !string.IsNullOrWhiteSpace(_imagePath);

            using var client = new HttpClient();

            if (hasImage)
            {
                using var content = new MultipartFormDataContent();
                var payload = new { content = hasStory ? story : string.Empty };
                var payloadJson = JsonSerializer.Serialize(payload);
                content.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");

                await using var stream = File.OpenRead(_imagePath!);
                var fileContent = new StreamContent(stream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "file", Path.GetFileName(_imagePath));

                var response = await client.PostAsync(webhookUrl, content);
                response.EnsureSuccessStatusCode();
            }
            else
            {
                var payload = new { content = story };
                var payloadJson = JsonSerializer.Serialize(payload);
                using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(webhookUrl, content);
                response.EnsureSuccessStatusCode();
            }

            SetStatus("Sent! Granny will see it soon.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to send: {ex.Message}", isError: true);
        }
        finally
        {
            ToggleSending(false);
        }
    }

    private void ToggleSending(bool isSending)
    {
        _sendButton.Enabled = !isSending;
        _chooseImageButton.Enabled = !isSending;
        _webhookTextBox.Enabled = !isSending;
        _storyTextBox.Enabled = !isSending;
    }

    private void SetStatus(string message, bool isError)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = isError ? Color.Firebrick : Color.DimGray;
    }
}
