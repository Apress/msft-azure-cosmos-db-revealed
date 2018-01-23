using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CosmosUniversity.Web.Models
{
    public static class Repository<T> where T : class
    {
        private static readonly string _endPoint = ConfigurationManager.AppSettings["CosmosDBEndPoint"];
        private static readonly string _authKey = ConfigurationManager.AppSettings["CosmosDBAuthKey"];
        private static readonly string _dbName = "cosmosuniversity";
        private static readonly string _collectionName = "student";
        private static DocumentClient client = GetNewDocumentClient();

        private static DocumentClient GetNewDocumentClient()
        {
            ConnectionPolicy _connectionPolicy = new ConnectionPolicy { EnableEndpointDiscovery = false };
            //_connectionPolicy.PreferredLocations.Add(LocationNames.CentralUS);
            //_connectionPolicy.PreferredLocations.Add(LocationNames.WestUS2);
            return new DocumentClient(new Uri(_endPoint), _authKey, _connectionPolicy);
        }

        public static async Task<IEnumerable<T>> GetStudentsAsync(Expression<Func<T, bool>> where)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(_dbName, _collectionName);
            FeedOptions feedOptions = new FeedOptions { MaxItemCount = -1, EnableCrossPartitionQuery = true };

            IDocumentQuery<T> students;
            
            if (where == null)
            {
                students = client.CreateDocumentQuery<T>(collectionUri, feedOptions)
                                               .AsDocumentQuery();
            }
            else
            {
                students = client.CreateDocumentQuery<T>(collectionUri, feedOptions)
                                               .Where(where)
                                               .AsDocumentQuery();
            }
                                    
            List<T> listOfStudents = new List<T>();
            while (students.HasMoreResults)
            {
                listOfStudents.AddRange(await students.ExecuteNextAsync<T>());
            }

            return listOfStudents;
        }

        public static async Task<IEnumerable<T>> GetStudentsAgeAsync()
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(_dbName, _collectionName);
            FeedOptions feedOptions = new FeedOptions { MaxItemCount = -1, EnableCrossPartitionQuery = true };

            string sqlStatement = "SELECT s.firstName, s.lastName, udf.studentAge(s.birthDate) AS studentAge FROM s";

            SqlQuerySpec querySpec = new SqlQuerySpec()
            {
                QueryText = sqlStatement,
            };

            IDocumentQuery<T> students = client.CreateDocumentQuery<T>(collectionUri, querySpec, feedOptions)
                                 .AsDocumentQuery();

            List<T> listOfStudents = new List<T>();
            while (students.HasMoreResults)
            {
                listOfStudents.AddRange(await students.ExecuteNextAsync<T>());
            }

            return listOfStudents;
        }

        public static async Task<IEnumerable<T>> GetStudentsSQLAsync(string filterBy, string filterValue, string sortBy, string sortOrder)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(_dbName, _collectionName);
            FeedOptions feedOptions = new FeedOptions { MaxItemCount = -1, EnableCrossPartitionQuery = true };

            string sqlStatement = "SELECT * FROM s";
            if (!string.IsNullOrEmpty(filterValue))
            {
                sqlStatement = sqlStatement + " WHERE s." + filterBy + " = @filterValue";
            }

            sqlStatement = sqlStatement + " ORDER BY s." + sortBy + " " + sortOrder.ToUpper();

            SqlQuerySpec querySpec = new SqlQuerySpec()
            {
                QueryText = sqlStatement,
                Parameters = new SqlParameterCollection()
                {
                    new SqlParameter("@filterValue", filterValue)
                }
            };

            IDocumentQuery<T> students = client.CreateDocumentQuery<T>(collectionUri, querySpec, feedOptions)
                                 .AsDocumentQuery();

            List<T> listOfStudents = new List<T>();
            while (students.HasMoreResults)
            {
                listOfStudents.AddRange(await students.ExecuteNextAsync<T>());
            }

            return listOfStudents;
        }

        public static async Task<IEnumerable<Student>> GetStudentsLINQAsync(string filterBy, string filterValue, string sortBy, string sortOrder)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(_dbName, _collectionName);
            FeedOptions feedOptions = new FeedOptions { MaxItemCount = -1, EnableCrossPartitionQuery = true };

            var linqQuery = from s in client.CreateDocumentQuery<Student>(collectionUri, feedOptions)
                            select s;

            if (!string.IsNullOrEmpty(filterValue))
            {
                switch (filterBy)
                {
                    case "city":
                        linqQuery = from s in client.CreateDocumentQuery<Student>(collectionUri, feedOptions)
                                    where s.City == filterValue
                                    select s;
                        break;
                    case "state":
                        linqQuery = from s in client.CreateDocumentQuery<Student>(collectionUri, feedOptions)
                                    where s.State == filterValue
                                    select s;
                        break;
                    case "postalCode":
                        var postalCode = Convert.ToInt32(filterValue);
                        linqQuery = from s in client.CreateDocumentQuery<Student>(collectionUri, feedOptions)
                                    where s.PostalCode == postalCode
                                    select s;
                        break;
                }
            }

            if (sortBy == "firstName")
            {
                linqQuery = sortOrder == "asc"
                            ? linqQuery.OrderBy(x => x.FirstName)
                            : linqQuery.OrderByDescending(x => x.FirstName);
            }
            else
            {
                linqQuery = sortOrder == "asc"
                            ? linqQuery.OrderBy(x => x.LastName)
                            : linqQuery.OrderByDescending(x => x.LastName);
            }

            IDocumentQuery<Student> students = linqQuery.AsDocumentQuery();

            List<Student> listOfStudents = new List<Student>();
            while (students.HasMoreResults)
            {
                listOfStudents.AddRange(await students.ExecuteNextAsync<Student>());
            }

            return listOfStudents;
        }

        public static async Task<T> GetStudentAsync(string id, int partitionKey)
        {
            if (string.IsNullOrEmpty(id))
                throw new ApplicationException("No student id specified");

            Uri documentUri = UriFactory.CreateDocumentUri(_dbName, _collectionName, id);
            try
            {
                RequestOptions requestOptions = new RequestOptions {
                    PartitionKey = new PartitionKey(partitionKey)
                };
                Document student = await client.ReadDocumentAsync(documentUri, requestOptions);
                return (T)(dynamic)student;
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                throw;
            }
        }

        public static async Task<Document> CreateStudentAsync(T student)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(_dbName, _collectionName);
            RequestOptions requestOptions = new RequestOptions { PreTriggerInclude = new List<string> { "preCreateStudentIdentifyGenius" } };
            return await client.CreateDocumentAsync(collectionUri, student, requestOptions);
        }

        public static async Task<Document> CreateStudentWithStoredProcAsync(T student)
        {
            Uri storedProcedureUri = UriFactory.CreateStoredProcedureUri(_dbName, _collectionName, "createStudent");
            var st = student as Student;
            
            RequestOptions requestOptions = new RequestOptions
            {
                PartitionKey = new PartitionKey(st.PostalCode),
                PreTriggerInclude = new List<string> { "pre" }
            };
            
            return await client.ExecuteStoredProcedureAsync<Document>(storedProcedureUri, requestOptions, student);
        }

        public static async Task<Document> ReplaceStudentAsync(T student, string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ApplicationException("No student id specified");

            Uri documentUri = UriFactory.CreateDocumentUri(_dbName, _collectionName, id);
            return await client.ReplaceDocumentAsync(documentUri, student);
        }

        public static async Task<Document> DeleteStudentAsync(string id, int partitionKey)
        {
            if (string.IsNullOrEmpty(id))
                throw new ApplicationException("No student id specified");

            RequestOptions requestOptions = new RequestOptions
            {
                PartitionKey = new PartitionKey(partitionKey)
            };
            Uri documentUri = UriFactory.CreateDocumentUri(_dbName, _collectionName, id);
            return await client.DeleteDocumentAsync(documentUri, requestOptions);
        }
    }
}