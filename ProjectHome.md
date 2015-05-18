## CDR (Call Detail Record) Decoder for Ericsson, Siemens D900 MSC ##

---

`This project includes the following components:`

### CDR.Decoder.Core.dll ###
`.Net 2.0 library. Contains all functions to work with ASN.1 CDR-files, work with:`
  * `Siemens D900 MSC - versions SR13 (CS-50) and earlier (CS-10) [SiemensD900.CDR.Definition.xml];`
  * `Ericsson - SEQUENCE OF UMTSGSMPLMNCallDataRecord [Ericsson.CDR.Definition.xml];`
  * `Ericsson Gateway GPRS Support Node (GGSN) [Ericsson_GGSN.CDR.Definition.xml];`
  * `Ericsson Serving GPRS Support Node R7 (SGSN R7) [Ericsson_SGSN_R7.CDR.Definition.xml].`

`Sample CDR.Decoder.Core.dll.config :`
```
<?xml version="1.0" encoding="utf-8" ?>
<XMLDefinitionFile>Ericsson_GGSN.CDR.Definition.xml</XMLDefinitionFile>
```

### CDR.Decoder.exe ###
`.Net 2.0 Windows Forms application. Decoding CDR-file with the record results in the log-file`

`Usage:`
```
CDR.Decoder.exe [/s] <cdr-file.cdr | /d:source-directory | /j:job-file.job>
 
 /s - Autorun decoding process. In the end of the decoding process, the program will return result-code.

 If the next argument - is the name of CDR-file, the decoder will create a new task with the settings by default to decode the file.

 /d: - Specify the directory that contains the CDR-files. You can use wildcards "." and "?" in path-string.

 /j: - Load the saved job settings.
```

http://cdr-decoder.googlecode.com/files/2243.PNG

### Changelog ###
```
Build 101112-2227 (Version 2.2)
+	Extended support of Ericsson SGSN CDR
!	IntegerParselet now support up to 64-bit signed integer
!	Fixed some errors

Build 101024-2203
+	Support for CDR files Ericsson Gateway GPRS Support Node (GGSN), Ericsson Serving GPRS Support Node R7 (SGSN R7)

Build 100316-2312
!	New version - 2.1 . A lot of changes. Support for CDR files Ericsoon, Siemens D900

Build 090728-1658
!	Fixed errors in IntegerParselet, UssdStringParselet, add secondCellId to CS-10

Build 090703-1711
+ 	secondCellId, dAId, msAccessRate, redirectionCounter, sMTransmissionResult, reasonForTermination, usedEmlppPriority, cUGInterlockCode, aOCParameter

Build 090423-1139
+	recordType, callTransactionType parselets with value-type by default "Type"
+	ussdString parselet now decode ussd string correctly
+	Change default prefix. Hex: 0x -> H' ; Bin: 0b -> B'

Build 090417-1530
*	First public version
```

### HOWTO ###
`If enabled formatting-mode (`**Farmatter Active = True**`), only defined elements will be printed. For each element You can choose the type of value that will be logged, the name of the column. The available element dependent on the chosen definition scheme:`

http://cdr-decoder.googlecode.com/files/2238.PNG

`To adjust the format of the output log-file, you can use additional parameters:` **Format String**, **Print Columns Header**`:`

http://cdr-decoder.googlecode.com/files/2241.PNG

http://cdr-decoder.googlecode.com/files/2244.PNG