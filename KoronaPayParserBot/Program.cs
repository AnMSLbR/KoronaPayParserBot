using KoronaPayParserBot;
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
Dictionary<long, UserRequest> requests = new Dictionary<long, UserRequest>();
List<string> countries;
var botClient = new TelegramBotClient(accessToken);
using CancellationTokenSource cts = new();

ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = new UpdateType[]
    {
        UpdateType.Message,
        UpdateType.CallbackQuery,
    }
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
    #region TextMessage
    if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text)
    {
        var chatId = update.Message.Chat.Id;
        var messageText = update.Message.Text.ToUpper();
        var firstName = update.Message.From.FirstName;
        countries = parser.GetCountries();
        Console.WriteLine(chatId + " " + firstName + ": " + messageText);

        if (messageText == "/START")
        {
            await botClient.SendTextMessageAsync(chatId, $"Select a destination country:\r\n/{String.Join("\r\n/", countries)}", cancellationToken: cancellationToken);
        }
        else if (messageText == "/LINK")
        {
            await botClient.SendTextMessageAsync(chatId, "https://koronapay.com/transfers/online", cancellationToken: cancellationToken);
        }
        else if (countries.Any(str => "/" + str == messageText))
        {
            if (requests.ContainsKey(chatId))
            {
                requests[chatId].Country = messageText;
            }
            else
            {
                requests.Add(chatId, new UserRequest() { Country = messageText });
            }
            var currencies = parser.GetCurrencies(requests[chatId].Country.Substring(1));

            InlineKeyboardButton[] currencyKeys = new InlineKeyboardButton[currencies.Count];
            for (int i = 0; i < currencies.Count; i++)
            {
                currencyKeys[i] = InlineKeyboardButton.WithCallbackData(text: currencies[i], callbackData: currencies[i] + messageText) ;
            }
            InlineKeyboardMarkup currencyKeyboard = new(new[]{currencyKeys});

            await botClient.SendTextMessageAsync(chatId, $"Destination country: {requests[chatId].Country} \r\nSelect a currency:", replyMarkup: currencyKeyboard, cancellationToken: cancellationToken);
        }
        else if (requests.ContainsKey(chatId) && requests[chatId].Currency != null)
        {
            try
            {
                requests[chatId].Amount = messageText;
                parser.Parse(requests[chatId].Country, requests[chatId].Currency, requests[chatId].Amount);
                string parsedInfo = CombineParsedInfo(requests[chatId].Country, requests[chatId].Currency);
                await botClient.SendTextMessageAsync(chatId: chatId,
                                                     text: parsedInfo,
                                                     parseMode: ParseMode.MarkdownV2,
                                                     replyMarkup: terminalKeyboard,
                                                     cancellationToken: cancellationToken);
                if (requests.ContainsKey(chatId))
                {
                    requests.Remove(chatId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await botClient.SendTextMessageAsync(chatId, "Bad request", cancellationToken: cancellationToken);
            }
        }
    }
    #endregion

    #region CallbackQuery
    if (update.Type == UpdateType.CallbackQuery)
    {
        var chatId = update.CallbackQuery.Message.Chat.Id;
        if (update.CallbackQuery.Data == "Update")
        {
            var entityValues = update.CallbackQuery.Message.EntityValues.ToArray();
            var country = entityValues[0].Substring(1);
            var currency = entityValues[1];
            var amount = entityValues[2].Split(',')[0];
            try
            {
                parser.Parse(country, currency, amount);
                string parsedInfo = CombineParsedInfo(country, currency);
                await botClient.EditMessageTextAsync(chatId: chatId,
                                                     update.CallbackQuery.Message.MessageId,
                                                     text: parsedInfo,
                                                     parseMode: ParseMode.MarkdownV2,
                                                     replyMarkup: terminalKeyboard,
                                                     cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await botClient.SendTextMessageAsync(chatId, "Bad request", cancellationToken: cancellationToken);
            }
        }
        else if (update.CallbackQuery.Data == "Return")
        {
            countries = parser.GetCountries();
            await botClient.SendTextMessageAsync(chatId, $"Select a destination country:\r\n/{String.Join("\r\n/", countries)}", cancellationToken: cancellationToken);
        }
        else 
        {
            var callbackData = update.CallbackQuery.Data.Split('/');
            if (requests.ContainsKey(chatId))
            {
                requests[chatId].Currency = callbackData[0];
                requests[chatId].Country = callbackData[1];
            }
            else
            {
                requests.Add(chatId, new UserRequest() { Currency = callbackData[0], Country = callbackData[1] });
            }

            await botClient.SendTextMessageAsync(chatId,
                                                 $"Destination country: /{requests[chatId].Country}\r\nSelected currency: __{requests[chatId].Currency}__\r\nEnter the transfer amount:",
                                                 parseMode: ParseMode.MarkdownV2,
                                                 cancellationToken: cancellationToken);
        }
    }
    #endregion
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
           $"Transfer amount: __{parser.GetReceivingAmount()}__ {parser.GetReceivingCurrency()}\r\n" +
           $"Total transfer amount: {parser.GetSendingAmount()} {parser.GetSendingCurrency()}\r\n";
}

 
