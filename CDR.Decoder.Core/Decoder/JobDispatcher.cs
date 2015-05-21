using System;
using System.ComponentModel;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

namespace CDR.Decoder
{
    public class JobDispatcher
    {
        private BackgroundWorker _worker;
        private IDecoderJob _job;
        private string _connectionString;
        private bool _overWrite;
        private JobStatus _status;
        private BasicLogger _logger;
        private bool _ready;

        public JobDispatcher()
            : this(null)
        {
        }
        // author: Jose Cohenca
        // constructor for sql case
        public JobDispatcher(IDecoderJob job, string connectionString, bool overWrite) 
            : this(job)
        {
            _connectionString = connectionString;
            _worker.DoWork += new DoWorkEventHandler(DoSqlJob);
            _overWrite = overWrite;
        }

        public JobDispatcher(IDecoderJob job)
        {
            _worker = new BackgroundWorker();
            //_worker.DoWork += new DoWorkEventHandler(DoJob);  //enable for output to file
            _worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(JobCompleted);
            _worker.ProgressChanged += new ProgressChangedEventHandler(JobProgressChanged);
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;
            _job = job;
            _status = new JobStatus();
            _logger = new BasicLogger();
            _ready = true;

            AssemblyVersionInfo vInfo = new AssemblyVersionInfo(typeof(JobBase));
            _logger.WriteLogMessage(String.Format("*** {0} {1}.{2} - (build {3})", vInfo.Title, vInfo.Version.Major, vInfo.Version.Minor, vInfo.LastBuildDate.ToString("yyMMdd-HHmm")), LogLevel.Info);

            string[] schemas = CDR.Schema.CdrDefinitionProvider.Instance.AvailableSchemas();
            if ((schemas != null) && (schemas.Length > 0))
            {
                _logger.WriteLogMessage(String.Format("*** Definition XML Version: {0}", CDR.Schema.CdrDefinitionProvider.Instance.XmlVersion), LogLevel.Info, false);
                _logger.WriteLogMessage("*** Available Schemas:", LogLevel.Info, false);
                foreach (string s in schemas)
                {
                    _logger.WriteLogMessage(String.Format("> {0}", s), LogLevel.Info, false);
                }

                byte[] sign = CDR.Schema.CdrDefinitionProvider.Instance.AviableSignatures();
                if ((sign != null) && (sign.Length > 0))
                {
                    _logger.WriteLogMessage(String.Format("*** Available Signatures: {0}", BitConverter.ToString(sign).Replace("-", "; ")), LogLevel.Info, false);
                }
                else
                {
                    _logger.WriteLogMessage("!!! Signatures definition not found, check definition.xml", LogLevel.Error, false);
                    _ready = false;
                }
            }
            else
            {
                _logger.WriteLogMessage("!!! Schemas definition not found, check definition.xml", LogLevel.Error, false);
                _ready = false;
            }
        }

        private void JobProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (this.OnJobProgressChanged != null)
                this.OnJobProgressChanged(sender, null);
        }

        private void JobCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _status.IsCompleted = true;
            if (this.AfterJobCompleted != null)
                this.AfterJobCompleted(sender, null);
        }

        private void DoJob(object sender, DoWorkEventArgs e)
        {
            
                if ((!_ready) || String.IsNullOrEmpty(_job.SourcePath))
                {
                    _logger.WriteLogMessage("*** Job Dispatcher not ready.", LogLevel.Info, true);
                    return;
                }

                _logger.WriteLogMessage("+++ Start new job:", LogLevel.Info);

                _status.ResultCode = JobResultCode.FatalError;

                CdrDecoder decoder = new CdrDecoder();
                decoder.ElementDefinitionProvider.CurrentSchema = _job.DefinitionSchemaName;
                CdrElement record;

                RecordFormatter formatter = (_job.IsFormatterActive && (_job.FormatterSettings != null)) ? new RecordFormatter(_job.FormatterSettings) : null;
                Regex filterRegex = null;
                if (_job.IsFilterActive && !String.IsNullOrEmpty(_job.FilterText))
                {
                    try
                    {
                        filterRegex = new Regex(_job.FilterText, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    }
                    catch (Exception error)
                    {
                        _logger.WriteLogMessage(String.Format("!!! A filter regular expression parsing error in FilterText occurred: {0}", error.Message), LogLevel.Error, true);
                        _status.ResultCode = JobResultCode.FatalError;
                        _logger.WriteLogMessage("+++ The job finished with an error.", LogLevel.Info);
                        return;
                    }
                }

                StreamWriter dstFile = new StreamWriter(_job.DestinationPath);
                if ((formatter != null) && _job.FormatterSettings.PrintColumnsHeader)
                {
                    dstFile.WriteLine(_job.FormatterSettings.ColumnsHeader);
                }

                FileInfo[] cdrFiles;

                cdrFiles = new DirectoryInfo(Path.GetDirectoryName(_job.SourcePath)).GetFiles(Path.GetFileName(_job.SourcePath), SearchOption.TopDirectoryOnly);
                FileStream cdr;
                long cdrLength;
                long rem;
                string recText;

                // SGSN patch ///////////////////////////////////////////////////////////

                bool sgsn = !String.IsNullOrEmpty(decoder.ElementDefinitionProvider.Type) && (String.Compare(decoder.ElementDefinitionProvider.Type, "SGSN", true) == 0);
                List<CdrElement> sgsnRecord = new List<CdrElement>();

                /////////////////////////////////////////////////////////////////////////

                _status.CdrFilesIn = cdrFiles.Length;
                _logger.WriteLogMessage(String.Format("Files to decode: {0}", _status.CdrFilesIn), LogLevel.Info, false);
                _logger.WriteLogMessage(String.Format("Schema: {0}", _job.DefinitionSchemaName), LogLevel.Info, false);
                _worker.ReportProgress(_status.Percent);
                foreach (FileInfo fi in cdrFiles)
                {
                    cdr = new FileStream(fi.FullName, FileMode.Open);
                    cdrLength = cdr.Length;

                    if (_job.StartOffset > 0) cdr.Seek(_job.StartOffset, SeekOrigin.Begin);

                    _status.RecordsOut = 0;
                    _status.CurrentCdrFile = fi.Name;
                    rem = 0;

                    if (sgsn)
                    {
                        sgsnRecord.Clear();
                    }

                    _logger.WriteLogMessage(String.Format("{0} ... ", fi.Name), LogLevel.Info);
                    _worker.ReportProgress(_status.Percent);
                    for (; ; )
                    {
                        if (_status.RecordsOut == 0)
                        {
                            if (sgsn && (sgsnRecord.Count > 0))
                            {
                                record = decoder.DecodeRecord(cdr, false);
                            }
                            else
                            {
                                record = decoder.DecodeRecord(cdr, true);
                            }
                        }
                        else
                        {
                            record = decoder.DecodeRecord(cdr, false);
                        }
                        if (record == null)
                        {
                            if (sgsn && (sgsnRecord.Count > 0))
                            {
                                _status.RecordsOut++;
                                _status.RecordsOutTotal++;
                                _status.Percent = (int)Math.Ceiling((double)cdr.Position / cdrLength * 100);

                                if (formatter == null)
                                {
                                    StringBuilder sgsnText = new StringBuilder(String.Format("{0,8} > {1} {2}=[", sgsnRecord[0].Offset, _status.RecordsOut, sgsnRecord[0].Name), sgsnRecord.Count + 1);
                                    for (int s = 1; s < sgsnRecord.Count; s++)
                                    {
                                        if (s > 1)
                                            sgsnText.Append(' ');
                                        sgsnText.Append(sgsnRecord[s].ToString());
                                    }

                                    sgsnText.Append("]");
                                    recText = sgsnText.ToString();
                                }
                                else
                                {
                                    recText = formatter.FormatSGSNRecord(sgsnRecord);
                                }

                                if ((filterRegex == null) || (filterRegex.Match(recText).Success))
                                    dstFile.WriteLine(recText);

                                Math.DivRem(_status.RecordsOut, 1000, out rem);
                                if (rem == 0) _worker.ReportProgress(_status.Percent);

                            }
                            break;
                        }

                        if (sgsn)
                        {
                            if (_worker.CancellationPending)
                            {
                                break;
                            }
                            if (record.IsConstructed && (record.Path.Equals("20") || record.Path.Equals("23") || record.Path.Equals("24")))
                            {
                                if (sgsnRecord.Count == 0)
                                {
                                    sgsnRecord.Add(record);
                                    continue;
                                }
                            }
                            else
                            {
                                sgsnRecord.Add(record);
                                continue;
                            }
                        }

                        _status.RecordsOut++;
                        _status.RecordsOutTotal++;
                        _status.Percent = (int)Math.Ceiling((double)cdr.Position / cdrLength * 100);

                        if (sgsn)
                        {
                            if (formatter == null)
                            {
                                StringBuilder sgsnText = new StringBuilder(String.Format("{0,8} > {1} {2}=[", sgsnRecord[0].Offset, _status.RecordsOut, sgsnRecord[0].Name), sgsnRecord.Count + 1);
                                for (int s = 1; s < sgsnRecord.Count; s++)
                                {
                                    if (s > 1)
                                        sgsnText.Append(' ');
                                    sgsnText.Append(sgsnRecord[s].ToString());
                                }

                                sgsnText.Append("]");
                                recText = sgsnText.ToString();
                            }
                            else
                            {
                                recText = formatter.FormatSGSNRecord(sgsnRecord);
                            }
                            sgsnRecord.Clear();
                            sgsnRecord.Add(record);
                        }
                        else
                        {
                            recText = (formatter == null) ? String.Format("{0,8} > {1} {2}", record.Offset, _status.RecordsOut, record.ToString()) : formatter.FormatRecord(record);
                        }

                        if ((filterRegex == null) || (filterRegex.Match(recText).Success))
                            dstFile.WriteLine(recText);

                        Math.DivRem(_status.RecordsOut, 1000, out rem);
                        if (rem == 0) _worker.ReportProgress(_status.Percent);
                        if (_worker.CancellationPending)
                        {
                            break;
                        }
                    }

                    cdr.Close();
                    _logger.AppendLogMessage(_status.RecordsOut.ToString());

                    if (_worker.CancellationPending)
                    {
                        break;
                    }
                    else
                    {
                        _status.CdrFilesIn--;
                        _status.CdrFilesOut++;
                        _status.Percent = 100;
                        _worker.ReportProgress(_status.Percent);
                    }
                }

                dstFile.Close();
                if (_worker.CancellationPending)
                {
                    _status.ResultCode = JobResultCode.CanceledByUser;
                    _logger.WriteLogMessage("+++ Process aborted by user.", LogLevel.Info);
                }
                else
                {
                    _status.ResultCode = JobResultCode.AllOK;
                    _logger.WriteLogMessage("+++ Decoding is successful done.", LogLevel.Info);
                }
            
        }






        private void DoSqlJob(object sender, DoWorkEventArgs e)
        {



            if ((!_ready) || String.IsNullOrEmpty(_job.SourcePath))
            {
                _logger.WriteLogMessage("*** Job Dispatcher not ready.", LogLevel.Info, true);
                return;
            }

            _logger.WriteLogMessage("+++ Start new job:", LogLevel.Info);

            _status.ResultCode = JobResultCode.FatalError;

            CdrDecoder decoder = new CdrDecoder();
            decoder.ElementDefinitionProvider.CurrentSchema = _job.DefinitionSchemaName;
            CdrElement record;

            //RecordFormatter formatter = null;//(_job.IsFormatterActive && (_job.FormatterSettings != null)) ? new RecordFormatter(_job.FormatterSettings) : null;
            //Regex filterRegex = null;

            FileInfo[] cdrFiles;

            cdrFiles = new DirectoryInfo(Path.GetDirectoryName(_job.SourcePath)).GetFiles(Path.GetFileName(_job.SourcePath), SearchOption.TopDirectoryOnly);
            FileStream cdr;
            long cdrLength;
            long rem;

            // SGSN patch ///////////////////////////////////////////////////////////

            bool sgsn = !String.IsNullOrEmpty(decoder.ElementDefinitionProvider.Type) && (String.Compare(decoder.ElementDefinitionProvider.Type, "SGSN", true) == 0);
            List<CdrElement> sgsnRecord = new List<CdrElement>();

            /////////////////////////////////////////////////////////////////////////

            _status.CdrFilesIn = cdrFiles.Length;
            _logger.WriteLogMessage(String.Format("Files to decode: {0}", _status.CdrFilesIn), LogLevel.Info, false);
            _logger.WriteLogMessage(String.Format("Schema: {0}", _job.DefinitionSchemaName), LogLevel.Info, false);
            _worker.ReportProgress(_status.Percent);

            Dictionary<string, DataTable> dict = new Dictionary<string, DataTable>();

            foreach (FileInfo fi in cdrFiles)
            {
                try
                {
                    cdr = new FileStream(fi.FullName, FileMode.Open);
                    cdrLength = cdr.Length;

                    if (_job.StartOffset > 0) cdr.Seek(_job.StartOffset, SeekOrigin.Begin);

                    _status.RecordsOut = 0;
                    _status.CurrentCdrFile = fi.Name;
                    rem = 0;

                    if (sgsn)
                    {
                        sgsnRecord.Clear();
                    }

                    _logger.WriteLogMessage(String.Format("{0} ... ", fi.Name), LogLevel.Info);
                    _worker.ReportProgress(_status.Percent);
                    for (; ; )
                    {
                        if (_status.RecordsOut == 0)
                        {
                            if (sgsn && (sgsnRecord.Count > 0))
                            {
                                record = decoder.DecodeRecord(cdr, false);
                            }
                            else
                            {
                                record = decoder.DecodeRecord(cdr, true);
                            }
                        }
                        else
                        {
                            record = decoder.DecodeRecord(cdr, false);
                        }
                        if (record == null)
                        {
                            if (sgsn && (sgsnRecord.Count > 0))
                            {
                                _status.RecordsOut++;
                                _status.RecordsOutTotal++;
                                _status.Percent = (int)Math.Ceiling((double)cdr.Position / cdrLength * 100);

                                foreach (CdrElement sgsnce in sgsnRecord)
                                {
                                    if (sgsnce.IsConstructed)
                                    {
                                        foreach (CdrElement cd in (List<CdrElement>)sgsnce.Value)
                                        {
                                            if (cd.IsConstructed && !cd.IsEmpty)
                                            {
                                                DataTable dt;
                                                if (!dict.TryGetValue(sgsnce.Name + "_" + cd.Name, out dt))
                                                {
                                                    dt = new DataTable();
                                                    dict.Add(sgsnce.Name + "_" + cd.Name, dt);
                                                }
                                                DataRow dr = dt.NewRow();

                                                foreach (CdrElement ce in cd.Value as List<CdrElement>)
                                                {
                                                    if (!dt.Columns.Contains(ce.Name))
                                                        dt.Columns.Add(ce.Name, typeof(string));
                                                    if (ce.IsConstructed)
                                                        dr[ce.Name] = ce.ToString().Remove(0, ce.Name.Length + 1);//ce.Value;
                                                    else
                                                        dr[ce.Name] = ce.Parselet.DefaultValue(ce);//ce.Value;
                                                }
                                                dt.Rows.Add(dr);
                                            }
                                        }
                                    }
                                }
                                //recText = formatter.FormatSGSNRecord(sgsnRecord);

                                Math.DivRem(_status.RecordsOut, 1000, out rem);
                                if (rem == 0) _worker.ReportProgress(_status.Percent);

                            }
                            break;
                        }

                        if (sgsn)
                        {
                            if (_worker.CancellationPending)
                            {
                                break;
                            }
                            if (record.IsConstructed && (record.Path.Equals("20") || record.Path.Equals("23") || record.Path.Equals("24")))
                            {
                                if (sgsnRecord.Count == 0)
                                {
                                    sgsnRecord.Add(record);
                                    continue;
                                }
                            }
                            else
                            {
                                sgsnRecord.Add(record);
                                continue;
                            }
                        }

                        _status.RecordsOut++;
                        _status.RecordsOutTotal++;
                        _status.Percent = (int)Math.Ceiling((double)cdr.Position / cdrLength * 100);

                        if (sgsn)
                        {
                            foreach (CdrElement sgsnce in sgsnRecord)
                            {
                                if (sgsnce.IsConstructed)
                                {
                                    foreach (CdrElement cd in (List<CdrElement>)sgsnce.Value)
                                    {
                                        if (cd.IsConstructed && !cd.IsEmpty)
                                        {
                                            DataTable dt;
                                            if (!dict.TryGetValue(sgsnce.Name + "_" + cd.Name, out dt))
                                            {
                                                dt = new DataTable();
                                                dict.Add(sgsnce.Name + "_" + cd.Name, dt);
                                            }
                                            DataRow dr = dt.NewRow();
                                            foreach (CdrElement ce in cd.Value as List<CdrElement>)
                                            {
                                                if (!dt.Columns.Contains(ce.Name))
                                                    dt.Columns.Add(ce.Name, typeof(string));
                                                if (ce.IsConstructed)
                                                    dr[ce.Name] = ce.ToString().Remove(0, ce.Name.Length + 1);//ce.Value;
                                                else
                                                    dr[ce.Name] = ce.Parselet.DefaultValue(ce);//ce.Value;
                                            }
                                            dt.Rows.Add(dr);
                                        }
                                    }
                                }
                            }
                            //recText = formatter.FormatSGSNRecord(sgsnRecord);

                            sgsnRecord.Clear();
                            sgsnRecord.Add(record);
                        }
                        else
                        {
                            if (record.IsConstructed)
                            {
                                List<CdrElement> lscd = (List<CdrElement>)record.Value;
                                CdrElement cd = lscd[0];
                                if (cd.IsConstructed && !cd.IsEmpty)
                                {
                                    DataTable dt;
                                    if (!dict.TryGetValue(record.Name + "_" + cd.Name, out dt))
                                    {
                                        dt = new DataTable();
                                        dict.Add(record.Name + "_" + cd.Name, dt);
                                    }
                                    DataRow dr = dt.NewRow();


                                    foreach (CdrElement ce in cd.Value as List<CdrElement>)
                                    {
                                        if (!dt.Columns.Contains(ce.Name))
                                            dt.Columns.Add(ce.Name, typeof(string));
                                        if (ce.IsConstructed)
                                            dr[ce.Name] = ce.ToString().Substring(ce.Name.Length + 2, ce.ToString().Length - ce.Name.Length - 3);//ce.Value;
                                        else
                                            dr[ce.Name] = ce.Parselet.DefaultValue(ce);//ce.Value;
                                    }

                                    string extrasLabel = "extras";
                                    if (!dt.Columns.Contains(extrasLabel))
                                        dt.Columns.Add(extrasLabel, typeof(string));
                                    dr[extrasLabel] = "";

                                    foreach (CdrElement ce in lscd)
                                    {
                                        if (ce != cd)
                                        {
                                            dr[extrasLabel] += (dr[extrasLabel].ToString().Length == 0 ? "" : ",") + ce.ToString();
                                        }
                                    }

                                    dt.Rows.Add(dr);
                                }

                            }
                            //recText = formatter.FormatRecord(record);
                        }



                        Math.DivRem(_status.RecordsOut, 1000, out rem);
                        if (rem == 0) _worker.ReportProgress(_status.Percent);
                        if (_worker.CancellationPending)
                        {
                            break;
                        }
                    }

                    cdr.Close();

                    _logger.WriteLogMessage(_status.RecordsOut.ToString(), LogLevel.Info);
                    //_logger.AppendLogMessage(_status.RecordsOut.ToString());

                    if (_worker.CancellationPending)
                    {
                        break;
                    }
                    else
                    {
                        _status.CdrFilesIn--;
                        _status.CdrFilesOut++;
                        _status.Percent = 100;
                        _worker.ReportProgress(_status.Percent);
                    }

                    if (_worker.CancellationPending)
                    {
                        _status.ResultCode = JobResultCode.CanceledByUser;
                        _logger.WriteLogMessage("+++ Process aborted by user.", LogLevel.Info);
                    }
                    else
                    {
                        _status.ResultCode = JobResultCode.AllOK;
                        _logger.WriteLogMessage("+++ Decoding is successful done.", LogLevel.Info);
                        WriteToSql(dict);
                        if (_overWrite) 
                            _overWrite = false;
                    }
                    dict.Clear();
                }
                catch (Exception ex)
                {
                    _status.ResultCode = JobResultCode.FatalError;
                    _logger.WriteLogMessage("Error :" + ex.Message + ". StackTrace : " + ex.StackTrace, LogLevel.Error);
                }
            }
        }

        private void CreateTable(string tableName, DataTable sourceTable)
        {
            string sqlsc = "";

            if (_overWrite) 
                sqlsc += "IF OBJECT_ID('" + tableName + "') IS NOT NULL DROP TABLE " + tableName + "\n";

            sqlsc += "IF OBJECT_ID('" + tableName + "') IS NULL \n";
            sqlsc += "CREATE TABLE " + tableName + "(";
            for (int i = 0; i < sourceTable.Columns.Count; i++)
            {
                sqlsc += "\n [" + sourceTable.Columns[i].ColumnName + "] ";
                sqlsc += " nvarchar(" + (sourceTable.Columns[i].MaxLength == -1 ? "max" : sourceTable.Columns[i].MaxLength.ToString()) + ") ";
                sqlsc += ",";
            }
            sqlsc = sqlsc.Substring(0, sqlsc.Length - 1) + ") \n";
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using(SqlCommand cmd = new SqlCommand(sqlsc,connection))
                {
                    cmd.ExecuteNonQuery();
                }
                connection.Close();
            }
             
        }

        private void AddColumntoSqlTable(SqlConnection conn, string tableName, string columnName, Type columnType)
        {
            string colType = "varchar(Max)";
            string cmdText = "ALTER TABLE " + tableName + " ADD [" + columnName + "] " + colType;
            _logger.WriteLogMessage(String.Format("Altered table : {0} ... ", cmdText), LogLevel.Info);

            using (SqlCommand cmd = new SqlCommand(cmdText, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void BulkCopyTable(string tableName, DataTable sourceTable)
        {
            using (SqlConnection destinationConnection = new SqlConnection(_connectionString))
            {
                destinationConnection.Open();
                string[] restrictions = new string[4] { null, null, tableName, null };
                DataTable columnList = destinationConnection.GetSchema("Columns", restrictions);
                Dictionary<string, Type> lsCol = new Dictionary<string, Type>();
                foreach (DataRow r in columnList.Rows)
                {
                    lsCol.Add(r["COLUMN_NAME"].ToString(),typeof(string));
                }

                // Set up the bulk copy object.
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection))
                {
                    foreach (DataColumn c in sourceTable.Columns)
                    {
                        Type coltype;
                        if (!lsCol.TryGetValue(c.ColumnName, out coltype))
                        {
                            AddColumntoSqlTable(destinationConnection, tableName,c.ColumnName, c.DataType);// ALTER TABLE "+tableName+" ADD COLUMN " +columnName+" " + columnType;
                        }  
                        bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(c.ColumnName, c.ColumnName));
                    }
                    bulkCopy.DestinationTableName = tableName;
                    bulkCopy.WriteToServer(sourceTable);
                }
                destinationConnection.Close();
            }
        }

        private void WriteToSql(Dictionary<string,DataTable> dict)
        {
            foreach (KeyValuePair<string, DataTable> kv in dict)
            {
                try
                {
                    string tableName = kv.Key;
                    DataTable sourceTable = kv.Value;
                    CreateTable(tableName, sourceTable);
                    BulkCopyTable(tableName, sourceTable);
                }
                catch (Exception ex)
                {
                    _status.ResultCode = JobResultCode.FatalError;
                    _logger.WriteLogMessage("Sql Table Error :" + ex.Message + ". StackTrace : " + ex.StackTrace, LogLevel.Error);
                    throw;
                }
            }
        }

        public event EventHandler AfterJobCompleted;
        public event EventHandler OnJobProgressChanged;

        public IDecoderJob Job
        {
            get { return _job; }
            set { _job = value; }
        }

        public BasicLogger Logger
        {
            get { return _logger; }
        }

        public JobStatus Status
        {
            get { return _status; }
        }

        public void ExecuteJob()
        {
            _status.IsRunning = true;
            _status.StartTime = DateTime.Now;
            _worker.RunWorkerAsync();
        }

        public void CancelJob()
        {
            _worker.CancelAsync();
        }
    }
}
