using Penguin.Cms.Logging.Entities;
using Penguin.Errors;
using Penguin.Messaging.Core;
using Penguin.Messaging.Logging;
using Penguin.Messaging.Logging.Extensions;
using Penguin.Persistence.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;

namespace Penguin.Logging
{
    /// <summary>
    /// A class provided with the intent of logging out messages through as many code paths as possible to ensure nothing is missed
    /// </summary>
    public class Logger : IDisposable
    {
        /// <summary>
        /// Constructs a new instance of this class
        /// </summary>
        /// <param name="caller">The object that is doing the logging</param>
        /// <param name="messageBus">A message bus to send log messages over</param>
        /// <param name="logEntryRepository">An IRepository implememntation for persisting logged messages</param>
        /// <param name="errorRepository">An IRepository implementation for persisting errors</param>
        public Logger(object caller, MessageBus messageBus, IRepository<LogEntry> logEntryRepository, IRepository<AuditableError> errorRepository)
        {
            Contract.Requires(logEntryRepository != null);
            Contract.Requires(caller != null);

            this.SessionStart = DateTime.Now;
            this.GUID = Guid.NewGuid().ToString();
            this.Caller = caller.GetType().ToString();
            this.Entries = new List<LogEntry>();
            this.MessageBus = messageBus;
            this.LogEntryRepository = logEntryRepository;
            this.ErrorRepository = errorRepository;
            this.FileName = $"Logs\\{Caller}.{SessionStart.ToString("yyyy.MM.dd.HH.mm.ss", CultureInfo.CurrentCulture)}.log";

            if (!Directory.Exists("Logs"))
            {
                Directory.CreateDirectory("Logs");
            }

            WriteContext = logEntryRepository.WriteContext();
        }

        private string FileName { get; set; }
        private IWriteContext WriteContext { get; set; }
        private string Caller { get; set; }
        private IRepository<LogEntry> LogEntryRepository { get; set; }
        private IRepository<AuditableError> ErrorRepository { get; set; }
        private List<LogEntry> Entries { get; set; }
        private string GUID { get; set; }
        private DateTime SessionStart { get; set; }
        private MessageBus MessageBus { get; set; }

        private void LogToFile(string toLog, LogLevel type)
        {
            try
            {
                File.AppendAllText(this.FileName, $"[{type}] {DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss", CultureInfo.CurrentCulture)}: {toLog}");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Logs a text string to the various recievers in this logger
        /// </summary>
        /// <param name="toLog">The pre-formatted string to log</param>
        /// <param name="type">The log level for this message</param>
        /// <param name="args">Any format arguments for the string</param>
        public void Log(string toLog, LogLevel type, params object[] args)
        {
            LogToFile(string.Format(CultureInfo.CurrentCulture, toLog, args), type);

            LogEntry newEntry = new LogEntry
            {
                Caller = this.Caller,
                Level = type,
                Session = this.GUID,
                SessionStart = this.SessionStart
            };

            MessageBus?.Log(string.Format(CultureInfo.CurrentCulture, toLog, args), type);

            if (args.Length > 0)
            {
                newEntry.Value = string.Format(CultureInfo.CurrentCulture, toLog, args);
            }
            else
            {
                newEntry.Value = toLog;
            }

            newEntry.DateCreated = DateTime.Now;

#if DEBUG
            System.Diagnostics.Debug.WriteLine("[" + DateTime.Now.ToString(CultureInfo.CurrentCulture) + "] " + newEntry.Value);
#else

#endif

            this.Entries.Add(newEntry);
        }

        /// <summary>
        /// Logs a debug level message to the various recievers
        /// </summary>
        /// <param name="toLog">The preformatted string to log</param>
        /// <param name="args">Optional arguments to be used during string formatting</param>
        public void LogDebug(string toLog, params object[] args) => this.Log(toLog, LogLevel.Debug, args);

        /// <summary>
        /// Logs an error level message to the various recievers
        /// </summary>
        /// <param name="toLog">The preformatted string to log</param>
        /// <param name="args">Optional arguments to be used during string formatting</param>
        public void LogError(string toLog, params object[] args) => this.Log(toLog, LogLevel.Error, args);

        /// <summary>
        /// Logs an exception as an exception level message to the various recievers
        /// </summary>
        /// <param name="ex">The exception to log</param>
        public void LogException(Exception ex)
        {
            Contract.Requires(ex != null);

            this.LogError(ex.Message);
            MessageBus?.Log(ex);
            this.ErrorRepository.AddOrUpdate(new AuditableError(ex));
        }

        /// <summary>
        /// Logs an informational message to the various recievers
        /// </summary>
        /// <param name="toLog">The preformatted string to log</param>
        /// <param name="args">Optional arguments to be used during string formatting</param>
        public void LogInfo(string toLog, params object[] args) => this.Log(toLog, LogLevel.Info, args);

        /// <summary>
        /// Logs an warning level message to the various recievers
        /// </summary>
        /// <param name="toLog">The preformatted string to log</param>
        /// <param name="args">Optional arguments to be used during string formatting</param>
        public void LogWarning(string toLog, params object[] args) => this.Log(toLog, LogLevel.Warning, args);

        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Disposes of this logger and persists messages to the underlying database for IRepository implementations
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of this logger and persists messages to the underlying database for IRepository implementations
        /// </summary>
        /// <param name="disposing">unused</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                this.LogEntryRepository.AddOrUpdate(this.Entries.ToArray());

                this.LogEntryRepository.Commit(WriteContext);

                this.disposedValue = true;
            }

            WriteContext.Dispose();
        }
    }
}