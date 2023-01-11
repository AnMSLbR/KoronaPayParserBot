using KoronaPayParserLib;
using System.Net.Http.Headers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

string accessToken = "YOUR_TOKEN";
var parser = new KoronaPayParser();
var botClient = new TelegramBotClient(accessToken);
using CancellationTokenSource cts = new();
ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

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
        var messageText = message.Text;
        var firstName = message.From.FirstName;
        var countries = parser.GetCountries();

        Console.WriteLine(chatId + " " + firstName + ": " + messageText);
        if (message.Text == "/start")
        {
            string countryString = "";
            foreach (var country in countries)
            {
                countryString += "/" + country + "\r\n";
            }
            Message sentMessage = await botClient.SendTextMessageAsync(chatId, $"Select a destination country:\r\n{countryString}", cancellationToken: cancellationToken);
        }
        else if (countries.Any(str => "/" + str == messageText.ToUpper()))
        {
            var currencies = parser.GetCurrencies(messageText.Substring(1).ToUpper());

            InlineKeyboardButton[] keys = new InlineKeyboardButton[currencies.Count];
            for (int i = 0; i < currencies.Count; i++)
            {
                keys[i] = InlineKeyboardButton.WithCallbackData(text: currencies[i], callbackData: currencies[i] + messageText.ToUpper()) ;
            }
            InlineKeyboardMarkup inlineKeyboard = new(new[]{keys});

            Message sentMessage = await botClient.SendTextMessageAsync(chatId, $"Destination country: {messageText.Substring(1).ToUpper()} \r\nSelect a currency:", replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
        }
    }

    if (update.Type == UpdateType.CallbackQuery)
    {
        var callbackData = update.CallbackQuery.Data.Split('/');
        var currency = callbackData[0];
        var country = callbackData[1];
        try
        {
            parser.Parse(country, currency);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        Message sentMessage = await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, $"Exchange rate: {parser.GetExchangeRate()}", cancellationToken: cancellationToken);
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

