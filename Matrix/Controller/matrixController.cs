
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Matrix.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class matrixController : ControllerBase
    {
        private static readonly HttpClient client = new HttpClient();
        public class MatrixRowResponse
        {
            public int[] Value { get; set; }
        }
        // Initialize datasets A and B
        [HttpGet("init/{size}")]
        public async Task<IActionResult> InitializeMatrices(int size)
        {
            var responseA = await client.GetAsync($"https://recruitment-test.investcloud.com/api/numbers/init/{size}");
            var responseB = await client.GetAsync($"https://recruitment-test.investcloud.com/api/numbers/init/{size}");

            if (responseA.IsSuccessStatusCode && responseB.IsSuccessStatusCode)
            {
                return Ok("Datasets initialized.");
            }

            return StatusCode(500, "Failed to initialize datasets.");
        }


        // Retrieve rows or columns of data from a dataset
        [HttpGet("{dataset}/{type}/{idx}")]
        public async Task<int[]> RetrieveMatrixData(string dataset, string type, int idx)
        {
            var response = await client.GetAsync($"https://recruitment-test.investcloud.com/api/numbers/{dataset}/{type}/{idx}");
            response.EnsureSuccessStatusCode();

            string jsonString = await response.Content.ReadAsStringAsync();
            var matrixRowResponse = JsonConvert.DeserializeObject<MatrixRowResponse>(jsonString);
            return matrixRowResponse.Value; 
        }


        // Multiply matrices and validate the result
        [HttpPost("validate")]
        public async Task<IActionResult> MultiplyAndValidateMatrices()
        {
            int size = 3;
            await InitializeMatrices(size);

            int[,] matrixA = new int[size, size];
            int[,] matrixB = new int[size, size];

            // Populate matrices A and B with data
            for (int i = 0; i < size; i++)
            {
                var rowA = await RetrieveMatrixData("A", "row", i);
                var rowB = await RetrieveMatrixData("B", "row", i);

                for (int j = 0; j < size; j++)
                {
                    matrixA[i, j] = rowA[j];
                    matrixB[i, j] = rowB[j];
                }
            }

            // Multiply matrices
            int[,] resultMatrix = MultiplyMatrices(matrixA, matrixB);

            // Convert result matrix to concatenated string
            string concatenatedString = ConvertMatrixToString(resultMatrix);

            // Compute MD5 hash of the concatenated string
            string md5Hash = ComputeMD5Hash(concatenatedString);

            // Submit the hash to the service
            var validationResponse = await SubmitHashToService(md5Hash);
            return Ok(new { ValidationResult = validationResponse });
        }
        // Multiply two matrices
        static int[,] MultiplyMatrices(int[,] matrixA, int[,] matrixB)
        {
            int size = matrixA.GetLength(0);
            int[,] resultMatrix = new int[size, size];

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    resultMatrix[i, j] = 0;
                    for (int k = 0; k < size; k++)
                    {
                        resultMatrix[i, j] += matrixA[i, k] * matrixB[k, j];
                    }
                }
            }

            return resultMatrix;
        }
        // Convert a matrix to a concatenated string
        static string ConvertMatrixToString(int[,] matrix)
        {
            StringBuilder sb = new StringBuilder();
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    sb.Append(matrix[i, j]);
                }
            }

            return sb.ToString();
        }

        // Compute MD5 hash of a string
        static string ComputeMD5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        // Submit the MD5 hash to the validation service
        static async Task<string> SubmitHashToService(string hash)
        {
            var content = new StringContent(JsonConvert.SerializeObject(new { hash }), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://recruitment-test.investcloud.com/api/numbers/validate", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(responseString);
            return result.value;
        }
    }
}
