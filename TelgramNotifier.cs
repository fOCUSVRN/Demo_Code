using CentralServer.Core;
using CentralServer.Db;
using CentralServer.Db.Models;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CentralServer.Telegram
{
    public class TelegramNotifier
    {
        private readonly IServiceScopeFactory _scope;
        private readonly AppOptions _opts;

        private const string SubscribeBtn = "Подписаться";
        private const string UnSubscribeBtn = "Отписаться";

        public TelegramBotClient TelegramCli { get; }

        public TelegramNotifier(IOptions<AppOptions> opts, IServiceScopeFactory scope)
        {
            _scope = scope;
            _opts = opts.Value;
            TelegramCli = new TelegramBotClient(opts.Value.AppSettings.Telegram.Token);
        }

        public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var message = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(message);
            return Task.CompletedTask;
        }

        private Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }

        private async Task TryAddUser(long chatId)
        {
            using var scope = _scope.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SqliteDbContext>();

            var dao = await db.TelegramUsers.FirstOrDefaultAsync(x => x.ChatId == chatId);

            if (dao == null)
            {
                dao = new TelegramUserDao
                {
                    Guid = Guid.NewGuid(),
                    ChatId = chatId,
                    Dts = DateTime.Now
                };

                await db.TelegramUsers.AddAsync(dao);
                await db.SaveChangesAsync();
            }
        }

        private async Task TryRemoveUser(long chatId)
        {
            using var scope = _scope.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SqliteDbContext>();

            var dao = await db.TelegramUsers.FirstOrDefaultAsync(x => x.ChatId == chatId);

            if (dao != null)
            {
                db.TelegramUsers.Remove(dao);
                await db.SaveChangesAsync();
            }
        }

        private async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
        {
            Console.WriteLine($"Receive message type: {message.Type}");
            if (message.Type != MessageType.Text)
                return;

            var msg = message.Text.ToUpper();

            if (msg.StartsWith("/"))
            {
                msg = msg.Remove(0, 1);
            }

            var response = string.Empty;


            if (msg == "START")
            {
                response = $"Добро пожаловать. Используйте команды";
            }

            else if (msg == SubscribeBtn.ToUpper())
            {
                await TryAddUser(message.Chat.Id);

                response = $"Вы были успешно подписаны";
            }

            else if (msg == UnSubscribeBtn.ToUpper())
            {
                await TryRemoveUser(message.Chat.Id);

                response = $"Вы были успешно отписаны";
            }

            else
                response = $"Неизвестная команда. Используйте команды";


            await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: response, replyMarkup: new ReplyKeyboardMarkup(new List<KeyboardButton>
            {
                new (SubscribeBtn),
                new (UnSubscribeBtn),
            }, true));
        }

        public async Task SendMessageFoAllSubscribers(string msg)
        {
            using var scope = _scope.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SqliteDbContext>();

            var chatIds = (await db.Database.GetDbConnection().QueryAsync<long>("SELECT ChatId FROM TelegramUsers")).ToList();

            foreach (var chat in chatIds)
            {
                try
                {
                    await TelegramCli.SendTextMessageAsync(chat,parseMode:ParseMode.Markdown, text: msg);
                }
                catch (Exception e)
                {
                   
                }
            }
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(botClient, update.Message),

                _ => UnknownUpdateHandlerAsync(botClient, update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        public async Task Init()
        {
            TelegramCli.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync));
        }
    }
}
