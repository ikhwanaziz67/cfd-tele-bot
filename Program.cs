using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

class Program
{
    static string BOT_TOKEN = "8514836785:AAGcL9IPjD7lzZczN5g1qfGisTM0IyiH1ZU";
    static string SPREADSHEET_ID = "1UkHGBQ7EqHpd4RFqQMzlDpz7FnYr4fUMEDyMRthiADI";
    static string SHEET_NAME = "cashflow";

    static ITelegramBotClient bot;
    static Dictionary<long, string> userState = new();
    static SheetsService sheetService;

    static async Task Main()
    {
        Console.WriteLine("Bot starting...");

        bot = new TelegramBotClient(BOT_TOKEN);
        InitGoogleSheets();

        using var cts = new CancellationTokenSource();

        bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            cancellationToken: cts.Token
        );

        Console.WriteLine("Bot is running...");
        await Task.Delay(Timeout.Infinite);
    }

    static void InitGoogleSheets()
{
    var json = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS");

    if (string.IsNullOrEmpty(json))
        throw new Exception("GOOGLE_CREDENTIALS env not found");

    GoogleCredential credential = GoogleCredential
        .FromJson(json)
        .CreateScoped(SheetsService.Scope.Spreadsheets);

    sheetService = new SheetsService(new BaseClientService.Initializer()
    {
        HttpClientInitializer = credential,
        ApplicationName = "Telegram Cashflow Bot"
    });
}

    static async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken ct)
    {
        if (update.Type == UpdateType.Message && update.Message!.Text != null)
        {
            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text;

            if (text == "/start")
            {
                await ShowMenu(chatId);
                return;
            }

            if (userState.ContainsKey(chatId))
            {
                if (decimal.TryParse(text, out decimal amount))
                {
                    SaveToSheet(userState[chatId], amount);
                    userState.Remove(chatId);

                    await botClient.SendMessage(
                        chatId,
                        $"✅ Saved RM {amount}\n\nPilih transaksi seterusnya 👇",
                        replyMarkup: MenuKeyboard()
                    );
                }
                else
                {
                    await botClient.SendMessage(chatId, "❌ Masukkan nombor sahaja");
                }
            }
        }

        if (update.Type == UpdateType.CallbackQuery)
        {
            var query = update.CallbackQuery!;
            var chatId = query.Message!.Chat.Id;

            if (query.Data == "IN")
            {
                userState[chatId] = "IN";
                await botClient.SendMessage(chatId, "💰 Masukkan jumlah CASH IN:");
            }

            if (query.Data == "OUT")
            {
                userState[chatId] = "OUT";
                await botClient.SendMessage(chatId, "💸 Masukkan jumlah CASH OUT:");
            }

            await botClient.AnswerCallbackQuery(query.Id);
        }
    }

    static Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception ex,
        CancellationToken ct)
    {
        Console.WriteLine(ex.ToString());
        return Task.CompletedTask;
    }

    static async Task ShowMenu(long chatId)
    {
        await bot.SendMessage(
            chatId,
            "📊 Pilih transaksi:",
            replyMarkup: MenuKeyboard()
        );
    }

    static InlineKeyboardMarkup MenuKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ Cash In", "IN"),
                InlineKeyboardButton.WithCallbackData("➖ Cash Out", "OUT")
            }
        });
    }

    static void SaveToSheet(string type, decimal amount)
    {
        var values = new List<object>
        {
            DateTime.Now.ToString("dd/MM/yyyy"),
            type == "IN" ? amount : "",
            type == "OUT" ? amount : ""
        };

        var body = new ValueRange
        {
            Values = new List<IList<object>> { values }
        };

        var request = sheetService.Spreadsheets.Values.Append(
            body,
            SPREADSHEET_ID,
            $"{"cashflow"}!A:C"
        );

        request.ValueInputOption =
            SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

        request.Execute();
    }
}
