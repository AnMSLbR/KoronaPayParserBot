using KoronaPayParserLib;
using Microsoft.VisualBasic;
using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

string accessToken = "YOUR_TOKEN";
var parser = new KoronaPayParser();
List<string> countries;
var botClient = new TelegramBotClient(accessToken);
using CancellationTokenSource cts = new();

ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

InlineKeyboardMarkup terminalKeyboard = new(new[]
{
    new [] {InlineKeyboardButton.WithCallbackData("Update")},
    new [] {InlineKeyboardButton.WithCallbackData("Return")},
});

botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);

var me = await botClient.GetMeAsync();
Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text)
    {
        var message = update.Message;
        var chatId = message.Chat.Id;
        var messageText = message.Text.ToUpper();
        var firstName = message.From.FirstName;
        countries = parser.GetCountries();
        Console.WriteLine(chatId + " " + firstName + ": " + messageText);

        if (messageText == "/START")
        {
            Message sentMessage = await botClient.SendTextMessageAsync(chatId, $"Select a destination country:\r\n/{String.Join("\r\n/", countries)}", cancellationToken: cancellationToken);
        }
        else if (messageText == "/LINK")
        {
            Message sentMessage = await botClient.SendTextMessageAsync(chatId, "https://koronapay.com/transfers/online", cancellationToken: cancellationToken);
        }
        else if (countries.Any(str => "/" + str == messageText))
        {
            var currencies = parser.GetCurrencies(messageText.Substring(1));

            InlineKeyboardButton[] currencyKeys = new InlineKeyboardButton[currencies.Count];
            for (int i = 0; i < currencies.Count; i++)
            {
                currencyKeys[i] = InlineKeyboardButton.WithCallbackData(text: currencies[i], callbackData: currencies[i] + messageText) ;
            }
            InlineKeyboardMarkup currencyKeyboard = new(new[]{currencyKeys});

            Message sentMessage = await botClient.SendTextMessageAsync(chatId, $"Destination country: {messageText} \r\nSelect a currency:", replyMarkup: currencyKeyboard, cancellationToken: cancellationToken);
        }
    }

    if (update.Type == UpdateType.CallbackQuery)
    {
        if (update.CallbackQuery.Data == "Update")
        {
            var entityValues = update.CallbackQuery.Message.EntityValues.ToArray();
            var country = entityValues[0].Substring(1);
            var currency = entityValues[1];
            try
            {
                parser.Parse(country, currency);
                string parsedInfo = CombineParsedInfo(country, currency);
                Message editMessage = await botClient.EditMessageTextAsync(update.CallbackQuery.Message.Chat.Id,
                                                                           update.CallbackQuery.Message.MessageId,
                                                                           text: parsedInfo,
                                                                           parseMode: ParseMode.MarkdownV2,
                                                                           replyMarkup: terminalKeyboard,
                                                                           cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Message sentMessage = await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, "Bad request", cancellationToken: cancellationToken);
            }
        }
        else if (update.CallbackQuery.Data == "Return")
        {
            countries = parser.GetCountries();
            Message sentMessage = await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, $"Select a destination country:\r\n/{String.Join("\r\n/", countries)}", cancellationToken: cancellationToken);
        }
        else 
        {
            var callbackData = update.CallbackQuery.Data.Split('/');
            var currency = callbackData[0];
            var country = callbackData[1];
            try
            {
                parser.Parse(country, currency);
                string parsedInfo = CombineParsedInfo(country, currency);
                Message sentMessage = await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                                                                           text: parsedInfo,
                                                                           parseMode: ParseMode.MarkdownV2,
                                                                           replyMarkup: terminalKeyboard,
                                                                           cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Message sentMessage = await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, "Bad request", cancellationToken: cancellationToken);
            }
        }
    }
}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };
    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}

string CombineParsedInfo(string country, string currency)
{
    var requestDateTime = $"{DateTime.UtcNow.Month.ToString("00")}\\-{DateTime.UtcNow.Day.ToString("00")}\\-{DateTime.UtcNow.Year.ToString("00")} " +
                   $"{DateTime.UtcNow.TimeOfDay.ToString("hh\\:mm\\:ss")}";
    return $"Date: {requestDateTime} UTC\r\n" +
           $"Destination country: /{country}\r\n" +
           $"Currency: __{currency}__\r\n" +
           $"Exchange rate: {parser.GetExchangeRate()}\r\n" +
           $"Transfer amount: {parser.GetReceivingAmount()} {parser.GetReceivingCurrency()}\r\n" +
           $"Total transfer amount: {parser.GetSendingAmount()} {parser.GetSendingCurrency()}\r\n";
}
 
