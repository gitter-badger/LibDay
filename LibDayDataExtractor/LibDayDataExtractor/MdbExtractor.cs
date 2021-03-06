﻿using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Text;

namespace LibDayDataExtractor
{
    /// <summary>
    /// Extracts contents of MDB files into TSV files.
    /// The MDB files are Microsoft Jet databases, that store data in tables.
    /// </summary>
    public class MdbExtractor
    {
        public void ExtractToTsv(string mdbFilePath, string outputDirectory)
        {
            using (OleDbConnection mdbConnection = ConnectToMdbFile(mdbFilePath))
            {
                foreach (string tableName in GetTableNames(mdbConnection))
                {
                    ExportTableToTsv(mdbFilePath, outputDirectory, mdbConnection, tableName);
                }
            }
        }

        private static void ExportTableToTsv(
            string mdbPath, string outputDirectory, OleDbConnection mdbConnection, string tableName)
        {
            string outputFileName = GenerateOutputPath(mdbPath, outputDirectory, tableName);

            Directory.CreateDirectory(outputDirectory);

            using (StreamWriter streamWriter = new StreamWriter(outputFileName))
            {
                string query = string.Format("Select * from [{0}]", tableName);
                using (OleDbCommand cmd = new OleDbCommand(query, mdbConnection))
                {
                    cmd.CommandType = CommandType.Text;

                    // TODO: need to add table headers

                    using (var dataReader = cmd.ExecuteReader())
                    {
                        while (dataReader.Read())
                        {
                            streamWriter.WriteLine(string.Join("\t", GetRowValues(dataReader)));
                        }
                    }
                }
            }
        }

        private static IEnumerable<string> GetRowValues(OleDbDataReader dataReader)
        {
            for (int index = 0; index < dataReader.FieldCount; index++)
            {
                yield return dataReader.GetValue(index).ToString();
            }
        }

        private static string GenerateOutputPath(
            string mdbPath, string outputDirectory, string tableName)
        {
            string fileName = string.Format("{0}-{1}.tsv",
                Path.GetFileNameWithoutExtension(mdbPath), tableName);

            return Path.Combine(outputDirectory, fileName);
        }

        private static OleDbConnection ConnectToMdbFile(string mdbPath)
        {
            OleDbConnectionStringBuilder sb = new OleDbConnectionStringBuilder();
            sb.Provider = "Microsoft.Jet.OLEDB.4.0";
            sb.PersistSecurityInfo = false;
            sb.DataSource = mdbPath;

            string password = GetPassword(mdbPath);
            if (!string.IsNullOrEmpty(password))
            {
                sb.Add("Jet OLEDB:Database Password", password);
            }

            OleDbConnection conn = new OleDbConnection(sb.ToString());
            conn.Open();
            return conn;
        }

        private static string GetPassword(string mdbPath)
        {
            byte[] key = { 0x86, 0xfb, 0xec, 0x37, 0x5d, 0x44, 0x9c, 0xfa, 0xc6, 0x5e, 0x28, 0xe6, 0x13, 0xb6 };
            byte[] password = new byte[14];

            using (FileStream file = File.OpenRead(mdbPath))
            {
                BinaryReader reader = new BinaryReader(file);

                int passwordLength = 0;
                for (passwordLength = 0; passwordLength < 14; passwordLength++)
                {
                    file.Seek(0x42 + passwordLength, SeekOrigin.Begin);

                    byte j = (byte)reader.ReadInt32();

                    j ^= key[passwordLength];

                    if (j != 0)
                    {
                        password[passwordLength] = j;
                    }
                    else
                    {
                        password[passwordLength] = 0;

                        break;
                    }
                }

                return Encoding.ASCII.GetString(password, 0, passwordLength);
            }
        }

        private static IEnumerable<string> GetTableNames(OleDbConnection conn)
        {
            DataTable schema = conn.GetSchema("Tables");

            foreach (DataRow dataRow in schema.Rows)
            {
                if (dataRow["TABLE_TYPE"].ToString() == "TABLE")
                {
                    yield return dataRow["TABLE_NAME"].ToString();
                }
            }
        }
    }
}
