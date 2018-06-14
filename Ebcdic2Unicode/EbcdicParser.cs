﻿using Ebcdic2Unicode.Constants;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;

namespace Ebcdic2Unicode
{
    public class EbcdicParser
    {
        public ParsedLine[] ParsedLines { get; private set; }

        #region Constructors

        //Constractor 1
        public EbcdicParser()
        {
            //Empty
        }

        //Constractor 2
        public EbcdicParser(string sourceFilePath, LineTemplate lineTemplate)
            : this(File.ReadAllBytes(sourceFilePath), lineTemplate)
        {
            //Read all file bytes and call 3rd constructor
        }

        //Constractor 3
        public EbcdicParser(byte[] allBytes, LineTemplate lineTemplate)
        {
            this.ParsedLines = this.ParseAllLines(lineTemplate, allBytes);
        }
        #endregion


        /// <summary>
        /// Parses multiple lines of binary data.
        /// </summary>
        /// <param name="lineTemplate">Template</param>
        /// <param name="allBytes">Source file bytes</param>
        /// <returns>Array of parsed lines</returns>
        public ParsedLine[] ParseAllLines(LineTemplate lineTemplate, byte[] allBytes)
        {
            Console.WriteLine("{0}: Parsing...", DateTime.Now);
            this.ValidateInputParameters(lineTemplate, allBytes, false);

            double expectedRows = (double)allBytes.Length / lineTemplate.LineSize;
            if (expectedRows % 1 == 0)
            {
                Console.WriteLine("{1}: Line count est {0:#,###.00}", expectedRows, DateTime.Now);
            }

            byte[] lineBytes = new byte[lineTemplate.LineSize];
            ParsedLine[] linesList = new ParsedLine[Convert.ToInt32(expectedRows)];
            ParsedLine parsedLine = null;
            int lineIndex = 0;

            for (int i = 0; i < allBytes.Length; i += lineTemplate.LineSize)
            {
                try
                {
                    Array.Copy(allBytes, i, lineBytes, 0, lineTemplate.LineSize);

                    parsedLine = this.ParseSingleLine(lineTemplate, lineBytes);

                    if (parsedLine != null)
                    {
                        linesList[lineIndex] = parsedLine;
                    }

                    lineIndex++;

                    if (lineIndex % 1000 == 0)
                    {
                        Console.Write(lineIndex + "\r");
                    }
                }
                catch (Exception ex)
                {
                    //Used for dubugging 
                    Console.WriteLine("Exception at line index {0}", lineIndex);
                    throw ex;
                }
            }
            Console.WriteLine("{1}: {0} line(s) have been parsed", linesList.Count(), DateTime.Now);
            return linesList;
        }

        /// <summary>
        /// Parses multiple lines of binary data.
        /// </summary>
        /// <param name="lineTemplate">Template</param>
        /// <param name="sourceFilePath">Source file path</param>
        /// <returns>Array of parsed lines</returns>
        public ParsedLine[] ParseAllLines(LineTemplate lineTemplate, string sourceFilePath)
        {
            Console.WriteLine("{1}: Reading {2}...", sourceFilePath, DateTime.Now);
            return this.ParseAllLines(lineTemplate, File.ReadAllBytes(sourceFilePath));
        }

        /// <summary>
        /// Parses multiple lines of binary data then writes it out to a specified destination in chunks.
        /// </summary>
        /// <param name="lineTemplate">Template</param>
        /// <param name="sourceFilePath">Source file path</param>
        /// <param name="writeOutputType">Output file type</param>
        /// <param name="outputFilePath">Output file path</param>
        /// <param name="chunkSize">Threshold in which to write output</param>
        /// <param name="includeColumnNames">Include column names in file</param>
        /// <param name="addQuotes">Add text quotes as part of output</param>
        /// <returns>boolean on completion</returns>
        public bool ParseAndWriteLines(LineTemplate lineTemplate, string sourceFilePath, string outputFilePath, WriteOutputType writeOutputType = WriteOutputType.Txt, bool includeColumnNames = true, bool addQuotes = true, int chunkSize = 100000)
        {
            try
            {

                using (FileStream reader = File.OpenRead(sourceFilePath))
                {
                    int fsBytes = (int)reader.Length;
                    int chunk = (lineTemplate.LineSize * chunkSize);
                    int loop = (int)(Math.Ceiling(((decimal)fsBytes / chunk)));
                    bool append = false;
                    int bytesRead = 0;

                    for (int i = 1; i <= loop; i++)
                    {
                        byte[] b = new byte[0];

                        Console.WriteLine($"Handling Batch {i} of {loop}");

                        if(bytesRead + chunk > fsBytes)
                        {
                            chunk = fsBytes - bytesRead;
                        }

                        b = new byte[chunk];
                        bytesRead += reader.Read(b, 0, chunk);

                        this.ParsedLines = ParseAllLines(lineTemplate, b);

                        switch (writeOutputType)
                        {
                            case WriteOutputType.Csv:
                                SaveParsedLinesAsCsvFile(outputFilePath, includeColumnNames, addQuotes, append);
                                break;
                            case WriteOutputType.Txt:
                                SaveParsedLinesAsTxtFile(outputFilePath, "|", includeColumnNames, addQuotes, "¬", append);
                                break;
                            case WriteOutputType.XML:
                                throw new NotImplementedException("XML Exporting in batches has not yet been implemented");
                        }
                        append = true;
                        Console.WriteLine($"--------------------------------------");
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Parses single line of binary data.
        /// </summary>
        /// <param name="lineTemplate">Template</param>
        /// <param name="lineBytes">Source bytes</param>
        /// <returns>Single parsed line</returns>
        public ParsedLine ParseSingleLine(LineTemplate lineTemplate, byte[] lineBytes)
        {
            bool isSingleLine = true;
            this.ValidateInputParameters(lineTemplate, lineBytes, isSingleLine);
            ParsedLine parsedLine = new ParsedLine(lineTemplate, lineBytes);
            return parsedLine;
        }

        private bool ValidateInputParameters(LineTemplate lineTemplate, byte[] allBytes, bool isSingleLine)
        {
            if (allBytes == null)
            {
                throw new ArgumentNullException(Messages.DataNotProvided);
            }
            if (lineTemplate == null)
            {
                throw new ArgumentNullException(Messages.LineTemplateNotProvided);
            }
            if (lineTemplate.FieldsCount == 0)
            {
                throw new Exception(Messages.LineTemplateHasNoFields);
            }
            if (allBytes.Length > 0 && allBytes.Length < lineTemplate.LineSize)
            {
                throw new Exception(Messages.DataShorterThanExpected);
            }
            if (isSingleLine && allBytes.Length != lineTemplate.LineSize)
            {
                throw new Exception(Messages.DataLengthDifferentThanExpected);
            }

            double expectedRows = (double)allBytes.Length / lineTemplate.LineSize;

            if (expectedRows % 1 != 0) //Expected number of rows is not a whole number
            {
                string errMsg = String.Format(Messages.ExpectedNumberOfRows, allBytes.Length, lineTemplate.LineSize, expectedRows);
                throw new Exception(errMsg);
            }

            return true;
        }

        public bool SaveParsedLinesAsCsvFile(string outputFilePath, bool includeColumnNames = true, bool addQuotes = true, bool append = false)
        {
            return ParserUtilities.WriteParsedLineArrayToCsv(this.ParsedLines, outputFilePath, includeColumnNames, addQuotes, false);
        }

        public bool SaveParsedLinesAsTxtFile(string outputFilePath, string delimiter = "\t", bool includeColumnNames = true, bool addQuotes = true, string quoteCharacter = "\"", bool append = false)
        {
            return ParserUtilities.WriteParsedLineArrayToTxt(this.ParsedLines, outputFilePath, delimiter, includeColumnNames, addQuotes, quoteCharacter, append);
        }

        public bool SaveParsedLinesAsXmlFile(string outputFilePath)
        {
            return ParserUtilities.WriteParsedLineArrayToXml(this.ParsedLines, outputFilePath);
        }
    }
}
