using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using System.Configuration;
using EFM.Api.Datatypes;
using System.Text.RegularExpressions;
using System.Reflection;
using EFM.Api.Helpers.Factory;

namespace EFM.Api
{
    public class SQLConnect : IDisposable
    {
        public SqlConnection SQLConnection;

        public SQLConnect()
        {
            SQLConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["DbConnection"].ConnectionString);
            SQLConnection.Open();
        }

        public void Dispose()
        {
            if (SQLConnection != null)
            {
                SQLConnection.Close();
                SQLConnection.Dispose();
            }
        }

        ~SQLConnect() { this.Dispose(); }
    }

    public class SQL : Singleton<SQL>
    {
        public List<T> Select<T>(string Query, object TObject = null) where T : IQueryAble,  new()
        {
            List<T> Collection = new List<T>();

            using (SQLConnect SQL = new SQLConnect())
            {
                /* SQLCommand is used for prepared statements, pass as argument to BuildCollection */

                using (SqlCommand command = BuildQuery<T>(Query, SQL.SQLConnection, TObject))
                {
                    command.Prepare();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        List<string> Columns = new List<string>();
                        List<PropertyInfo> Info = new List<PropertyInfo>();

                        for (int i = 0; i <= reader.FieldCount -1 ; i++) // not zero index based!
                            Columns.Add(reader.GetName(i));

                        foreach (string Column in Columns)
                            Info.Add(KST.ReflectionInfo.SingleOrDefault(x => x.Name == Column));

                        while (reader.Read()){
                            Collection.Add(CreateEntry<T>(Info, reader));
                        }
                    }
                }
            }

            return Collection;
        }
        public bool Update<T>(string Query, object Data) where T : IQueryAble, new() { return RunQuery<T>(Query, Data); }
        public bool Insert<T>(string Query, object Data) where T : IQueryAble, new() { return RunQuery<T>(Query, Data); }

        public bool RunQuery<T>(string Query, object Data) where T : IQueryAble, new()
        {
            using (SQLConnect SQL = new SQLConnect())
            {
                using (SqlCommand command = BuildQuery<T>(Query, SQL.SQLConnection, Data))
                {
                    command.Prepare();
                    return command.ExecuteNonQuery() != 0;
                }
            }
        }

        public T CreateEntry<T>(List<PropertyInfo> Props, SqlDataReader reader) where T : IQueryAble, new()
        {
            T Collection = new T();

            foreach (var x in Props)
                if (reader[x.Name] != DBNull.Value)
                    x.SetValue(Collection, reader[x.Name]);
                
            return Collection;
        }

        // Api response
   
        public SqlCommand BuildQuery<T>(string Query, SqlConnection Connection, object TObject) where T : IQueryAble, new()
        {
            SqlCommand Command = new SqlCommand(Query, Connection);

            Regex reg = new Regex(@"(?<=@)\w+");
            T Collection = new T();

            PropertyInfo[] PropertyInfo = (TObject is DatatypesBase) 
                    ? (TObject as DatatypesBase)._ReflectionInfo 
                    : TObject.GetType().GetProperties();

            foreach (Match i in reg.Matches(Command.CommandText))
            {
                string Value = i.Value;
                DataTypeAttribute Attrib = Collection[Value];

                if(Attrib != null)
                {
                    SqlParameter Param = new SqlParameter(string.Format("@{0}", Value), Attrib.type, Attrib.size);
                    Command.Parameters.Add(Param).Value = PropertyInfo.SingleOrDefault(x => x.Name == Value).GetValue(TObject);
                }
            }

            return Command;
        }
    }
}
