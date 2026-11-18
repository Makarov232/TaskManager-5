using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace TaskManagerApp
{
    internal class Program
    {
        private static readonly List<string> Tasks = new();

        private static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("Application", "TaskManager")
                .Enrich.WithProperty("Environment", "Development")
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] " +
                    "{Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    formatter: new CompactJsonFormatter(),
                    path: "logs/taskmanager-.json",
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("Программа TaskManager запущена");

                bool isRunning = true;

                while (isRunning)
                {
                    Console.Write(
                        "\nКоманда (add, remove, list, testerror, exit): ");

                    string? input = Console.ReadLine();

                    if (input is null)
                    {
                        Log.Information("Поток ввода закрыт");
                        break;
                    }

                    string command = input.Trim().ToLowerInvariant();

                    Log.Debug("Введена команда {Command}", command);

                    switch (command)
                    {
                        case "add":
                            AddTask();
                            break;

                        case "remove":
                            RemoveTask();
                            break;

                        case "list":
                            ListTasks();
                            break;

                        case "testerror":
                            TestError();
                            break;

                        case "exit":
                            isRunning = false;
                            break;

                        default:
                            Log.Warning(
                                "Неизвестная команда {Command}",
                                command);

                            Console.WriteLine("Неизвестная команда.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
 
                HandleException(
                    ex,
                    operation: "Main",
                    level: LogEventLevel.Fatal);

                Environment.ExitCode = 1;
            }
            finally
            {
                Log.Information("Программа TaskManager завершена");
                Log.CloseAndFlush();
            }
        }

        /// Единая точка логирования и уведомления об исключениях.
        private static void HandleException(
            Exception exception,
            string operation,
            Guid? operationId = null,
            LogEventLevel level = LogEventLevel.Error)
        {
            Log.ForContext("Operation", operation)
                .ForContext("OperationId", operationId)
                .ForContext(
                    "ExceptionType",
                    exception.GetType().FullName)
                .ForContext(
                    "ExceptionMessage",
                    exception.Message)
                .ForContext(
                    "StackTrace",
                    exception.StackTrace ?? "Стек вызовов отсутствует")
                .ForContext("ErrorLevel", level.ToString())
                .Write(
                    level,
                    exception,
                    "Исключение в операции {Operation}: {ExceptionMessage}",
                    operation,
                    exception.Message);

            Console.WriteLine(
                $"!!! Ошибка !!! Операция: {operation}. " +
                "Подробности записаны в журнал.");

        }

        /// Общая трассировка и перехват исключений операций.
        private static void TraceOperation(
            string operation,
            Func<(LogEventLevel Level, string Result)> action)
        {
            Guid operationId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            LogEventLevel level = LogEventLevel.Information;
            string result = "Успех";

            Log.Debug(
                "Начало операции {Operation}. " +
                "OperationId: {OperationId}. Этап: {Stage}",
                operation,
                operationId,
                "Start");

            try
            {
                var outcome = action();

                level = outcome.Level;
                result = outcome.Result;
            }
            catch (Exception ex)
            {
                level = LogEventLevel.Error;
                result = "Ошибка";

                HandleException(
                    ex,
                    operation,
                    operationId,
                    level);
            }
            finally
            {
                stopwatch.Stop();

                Log.Write(
                    level,
                    "Конец операции {Operation}. " +
                    "OperationId: {OperationId}. Этап: {Stage}. " +
                    "Результат: {Result}. " +
                    "Время: {DurationMs:F3} мс. " +
                    "Количество задач: {TaskCount}",
                    operation,
                    operationId,
                    "End",
                    result,
                    stopwatch.Elapsed.TotalMilliseconds,
                    Tasks.Count);
            }
        }

        private static void AddTask()
        {
            TraceOperation("Add", () =>
            {
                Console.Write("Введите название задачи: ");
                string? taskName = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(taskName))
                {
                    Console.WriteLine("Название не может быть пустым.");

                    return (
                        LogEventLevel.Warning,
                        "Предупреждение: пустое название");
                }

                Tasks.Add(taskName);

                Log.Debug("Добавлена задача {TaskName}", taskName);
                Console.WriteLine("Задача добавлена.");

                return (
                    LogEventLevel.Information,
                    "Успех: задача добавлена");
            });
        }

        private static void RemoveTask()
        {
            TraceOperation("Remove", () =>
            {
                Console.Write("Введите название задачи для удаления: ");
                string? taskName = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(taskName))
                {
                    Console.WriteLine("Название не указано.");

                    return (
                        LogEventLevel.Warning,
                        "Предупреждение: пустое название");
                }

                if (!Tasks.Remove(taskName))
                {
                    Log.Debug("Задача {TaskName} не найдена", taskName);
                    Console.WriteLine("Задача не найдена.");

                    return (
                        LogEventLevel.Warning,
                        "Предупреждение: задача не найдена");
                }

                Log.Debug("Удалена задача {TaskName}", taskName);
                Console.WriteLine("Задача удалена.");

                return (
                    LogEventLevel.Information,
                    "Успех: задача удалена");
            });
        }

        private static void ListTasks()
        {
            TraceOperation("List", () =>
            {
                if (Tasks.Count == 0)
                {
                    Console.WriteLine("Список задач пуст.");

                    return (
                        LogEventLevel.Information,
                        "Успех: список задач пуст");
                }

                Console.WriteLine("Список задач:");

                for (int i = 0; i < Tasks.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {Tasks[i]}");
                }

                return (
                    LogEventLevel.Information,
                    "Успех: список выведен");
            });
        }

        private static void TestError()
        {
            TraceOperation("TestError", () =>
            {
                throw new InvalidOperationException(
                    "Тестовое исключение для проверки обработчика.");
            });
        }
    }
}