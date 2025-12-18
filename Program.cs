using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ClosedXML.Excel;

class Program
{
    // 🔑 TOKEN (yang kau bagi)
    static string BOT_TOKEN = "8514836785:AAGcL9IPjD7lzZczN5g1qfGisTM0IyiH1ZU";

    static TelegramBotClient bot = new TelegramBotClient(BOT_TOKEN);

    // Simpan state user
    static Dictionary<long, string> userState = new();

    static string excelPath = "cashflow.xlsx";

    static async Task Main()
    {
        Console.WriteLine("Bot running...");

        if (!File.Exists(excelPath))
            CreateExcel();

        using var cts = new CancellationTokenSource();

        bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            cancellationToken: cts.Token
        );

        Console.ReadLine();
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
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
                    SaveToExcel(userState[chatId], amount);
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

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception ex, CancellationToken ct)
    {
        Console.WriteLine(ex.Message);
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

    // ================= EXCEL =================

    static void CreateExcel()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("CashFlow");

        ws.Cell(1, 1).Value = "Date";
        ws.Cell(1, 2).Value = "IN";
        ws.Cell(1, 3).Value = "OUT";

        wb.SaveAs(excelPath);
    }

    static void SaveToExcel(string type, decimal amount)
    {
        using var wb = new XLWorkbook(excelPath);
        var ws = wb.Worksheet(1);

        int row = ws.LastRowUsed()?.RowNumber() + 1 ?? 2;

        ws.Cell(row, 1).Value = DateTime.Now.ToString("dd/MM/yyyy");

        if (type == "IN")
            ws.Cell(row, 2).Value = amount;
        else
            ws.Cell(row, 3).Value = amount;

        wb.Save();
    }
}
