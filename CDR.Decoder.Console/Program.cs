using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Threading.Tasks;
using CDR.Decoder.BER;
using CDR.Schema;
using CDR.Decoder.Parselets;
using log4net;
using log4net.Config;

namespace CDR.Decoder.Console
{
    public class DecoderJob : JobBase
    {
        public override JobDecodingMode DecodingMode
        {
            get
            {
                return base.DecodingMode;
            }
            set
            {
                base.DecodingMode = value;
            }
        }

        
        public override string DestinationPath
        {
            get
            {
                return base.DestinationPath;
            }
            set
            {
                base.DestinationPath = value;
            }
        }

        
        public override bool IsFilterActive
        {
            get
            {
                return base.IsFilterActive;
            }
            set
            {
                base.IsFilterActive = value;
            }
        }

        
        public override bool IsFormatterActive
        {
            get
            {
                return base.IsFormatterActive;
            }
            set
            {
                base.IsFormatterActive = value;
            }
        }

        public override RecordFormatterSettings FormatterSettings
        {
            get
            {
                return base.FormatterSettings;
            }
            set
            {
                base.FormatterSettings = value;
            }
        }

        
        public override string DefinitionSchemaName
        {
            get
            {
                return base.DefinitionSchemaName;
            }
            set
            {
                base.DefinitionSchemaName = value;
            }
        }
    }


    public class Program
    {
        private static JobDispatcher _dispatcher;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));

        public static void Log_MessagesChanged(object sender, EventArgs e)
        {
            List<LogMessage> lm = new List<LogMessage>();
            lm.AddRange(_dispatcher.Logger.LastMessages);
            foreach(LogMessage l in lm)
            {
                if (log.IsInfoEnabled && l.Level == LogLevel.Info) log.Info(string.Format("Timestamp: {0}. Level: {1}. Message: {2}", l.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"), l.Level.ToString(), l.Message));
                else if (log.IsErrorEnabled && l.Level == LogLevel.Error) log.Error(string.Format("Timestamp: {0}. Level: {1}. Message: {2}", l.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"), l.Level.ToString(), l.Message));
                else if (log.IsDebugEnabled && l.Level == LogLevel.Debug0) log.Debug(string.Format("Timestamp: {0}. Level: {1}. Message: {2}", l.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"), l.Level.ToString(), l.Message));
                else if (log.IsFatalEnabled && l.Level == LogLevel.Debug1) log.Fatal(string.Format("Timestamp: {0}. Level: {1}. Message: {2}", l.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"), l.Level.ToString(), l.Message));
                else if (log.IsWarnEnabled && l.Level == LogLevel.Warning) log.Warn(string.Format("Timestamp: {0}. Level: {1}. Message: {2}", l.TimeStamp.ToString("yyyy/MM/dd HH:mm:ss"), l.Level.ToString(), l.Message));
            }
            
        }

        public static void Dispatcher_AfterJobCompleted(object sender, EventArgs e)
        {
            string finished = "";
            int key = 0;
            if(finished=="") key=1;
        }

        public static void Dispatcher_OnJobProgressChanged(object sender, EventArgs e)
        {
            string finished = "";
            int key = 0;
            if (finished == "") key = 1;
        }



        static void Main(string[] args)
        {
            string SourcePath = ConfigurationManager.AppSettings["SourcePath"];// @"C:\Users\josecohenca\Downloads\cellmining\cdr\data\TTFILE00_8566";
            string connectionString = ConfigurationManager.AppSettings["connectionString"];// "Data Source=localhost; Initial Catalog=CDR;Integrated Security=SSPI;Connection Timeout=1800;";
            bool overWriteTables = bool.Parse(ConfigurationManager.AppSettings["overWriteTables"]);// true;
            string XMLDefinitionFile = ConfigurationManager.AppSettings["XMLDefinitionFile"];//@"C:\dev\cdr-decoder\CDR.Decoder.Core\Ericsson.CDR.Definition.xml";
            DecoderJob job = null;
            bool startJob = true;
            bool _autorun;
            XmlConfigurator.Configure();

            job = new DecoderJob();
            job.DecodingMode = JobDecodingMode.BatchDecoding;
            job.DefinitionSchemaName = CdrDefinitionProvider.Instance.CurrentSchema;
            job.SourcePath = SourcePath;
            //job.DestinationPath = String.Concat(SourcePath, ".dump");
            //job.FormatterSettings = new RecordFormatterSettings(RecordFormatterSettings.DefaultFormatString);
            _dispatcher = new JobDispatcher(job, connectionString, overWriteTables);
            _dispatcher.Logger.MessagesChanged += new EventHandler(Log_MessagesChanged);

#if DEBUG
            _dispatcher.Logger.LogLevel = LogLevel.Debug0;
#endif
            //_dispatcher.OnJobProgressChanged += new EventHandler(Dispatcher_OnJobProgressChanged);
            //_dispatcher.AfterJobCompleted += new EventHandler(Dispatcher_AfterJobCompleted);
            
            _dispatcher.Status.Reset();
            _dispatcher.ExecuteJob();

            while (_dispatcher.Status.IsRunning == true)
                System.Threading.Thread.Sleep(1000);


        }

    }
}
