using Microsoft.AspNetCore.Mvc;
using System.Security;

using Oci.Common;
using Oci.Common.Auth;
using Oci.Common.Utils;

using Oci.ObjectstorageService;
using Oci.ObjectstorageService.Models;
using Oci.ObjectstorageService.Requests;
using Oci.ObjectstorageService.Responses;

using Oci.AivisionService;
using Oci.AivisionService.Requests;
using Oci.AivisionService.Models;
using Org.BouncyCastle.Bcpg.Sig;

namespace DemoAIVision02.Controllers
{
    public class ImageController : Controller
    {
        private readonly IConfiguration pobjConfiguration;
        private readonly IWebHostEnvironment pobjWebHostingConfiguration;
        private string RandomizeFileName(string AdvisorName, string FileName)
        {
            //Gera um numero randomico para evitar conflito de nomes
            Random objR = new Random();
            int intRandomNumber = objR.Next();

            //Une o Arquivo com o Nome do Estudante e o numero randomico do momento
            string strResult = AdvisorName + "-" + intRandomNumber.ToString() + "-" + FileName;

            return strResult;
        }
        public string BuildUpUrl(string FileName) 
        {
            string strUrl = "https://objectstorage." + pobjConfiguration.GetValue<string>("OCIBucket:RegionId") + ".oraclecloud.com/n/" + pobjConfiguration.GetValue<string>("OCIBucket:Repository") + "/b/" +
                pobjConfiguration.GetValue<string>("OCIBucket:BucketName") + "/o/" + pobjConfiguration.GetValue<string>("OCIBucket:FolderName") +
                "/" + FileName;

            return strUrl;
        }
        public ImageController(IConfiguration objConfiguration, IWebHostEnvironment env)
        {
            pobjConfiguration = objConfiguration;
            pobjWebHostingConfiguration = env;
        }
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Upload(List<IFormFile> files, string advisorname)
        {

            //Pega o Arquivo
            var objFile = files.FirstOrDefault();

            // Se alguem passou algum arquivo
            if (objFile != null & advisorname!= null)
            {
                string strFileName = RandomizeFileName(advisorname, objFile.FileName);
                //Armaze na para uso na View
                ViewBag.OK = true;
                ViewBag.FileName = strFileName;
                ViewBag.Size = objFile.Length;

                if (objFile.Length > 0)
                {
                    //
                    // Upload para OCI
                    //

                    // Cria o cliente para acessar o Container/Repositorio no OCI 
                    // As credenciais estão armazenados no appsettings.json
                    var provider = new SimpleAuthenticationDetailsProvider();
                    provider.UserId = pobjConfiguration.GetValue<string>("OCIBucket:UserId");
                    provider.Fingerprint = pobjConfiguration.GetValue<string>("OCIBucket:Fingerprint");
                    provider.TenantId = pobjConfiguration.GetValue<string>("OCIBucket:TenantId");
                    provider.Region = Region.FromRegionId(pobjConfiguration.GetValue<string>("OCIBucket:RegionId"));
                    SecureString passPhrase = StringUtils.StringToSecureString(pobjConfiguration.GetValue<string>("OCIBucket:PassPhrase"));
                    provider.PrivateKeySupplier = new FilePrivateKeySupplier(pobjWebHostingConfiguration.ContentRootPath + "/" + pobjConfiguration.GetValue<string>("OCIBucket:PrivateKeyFile"), passPhrase);

                    // Cria o cliente para o Object Storage
                    var osClient = new ObjectStorageClient(provider, new ClientConfiguration());
                    var getNamespaceRequest = new GetNamespaceRequest();
                    var namespaceRsp = await osClient.GetNamespace(getNamespaceRequest);
                    var ns = namespaceRsp.Value;


                    using (var stream = objFile.OpenReadStream())
                    {
                        // Estabelece informações do upload (nome, local)
                        var putObjectRequest = new PutObjectRequest()
                        {
                            BucketName = pobjConfiguration.GetValue<string>("OCIBucket:BucketName"),
                            NamespaceName = ns,
                            ObjectName = pobjConfiguration.GetValue<string>("OCIBucket:FolderName") + "/" + strFileName,
                            PutObjectBody = stream
                        };

                        //Upload do Arquivo
                        var putObjectRsp = await osClient.PutObject(putObjectRequest);
                    }

                    //
                    // Analisando a imagem
                    //
                    var strUrl = BuildUpUrl(strFileName);
                    var osAIClient = new AIServiceVisionClient(provider, new ClientConfiguration());

                    //Preenchimento dos dados
                    // Hierarquia
                    // AnalyzeImageRequest <- AnalyzeImageDetails <- ImageClassificationFeature

                    // Dados da Imagem
                    ImageClassificationFeature objImageClassificationFeature = new ImageClassificationFeature();
                    ObjectStorageImageDetails objObjectStorageImageDetails = new ObjectStorageImageDetails()
                    {
                        BucketName = pobjConfiguration.GetValue<string>("OCIBucket:BucketName"),
                        NamespaceName = pobjConfiguration.GetValue<string>("OCIBucket:Repository"),
                        ObjectName = pobjConfiguration.GetValue<string>("OCIBucket:FolderName") + "/" + strFileName
                    };
                    AnalyzeImageDetails objImageDetails = new AnalyzeImageDetails()
                    {
                        CompartmentId = pobjConfiguration.GetValue<string>("OCIBucket:CompartmentId"),
                        Image = objObjectStorageImageDetails
                    };
                    objImageDetails.Features = new List<ImageFeature>();
                    objImageDetails.Features.Add(objImageClassificationFeature);

                    // Preenche a requisição
                    var objRequest = new AnalyzeImageRequest()
                    {
                        AnalyzeImageDetails = objImageDetails
                    };

                    // Submete para avaliação
                    var putAIVisionRsp = await osAIClient.AnalyzeImage(objRequest);

                    ViewBag.OK = true;
                    ViewBag.FileName = objFile.FileName;
                    ViewBag.Url = strUrl;
                    ViewBag.Labels = putAIVisionRsp.AnalyzeImageResult.Labels;

                }
            }
            else
            {
                //Sinaliza que não há arquivo
                ViewBag.OK = false;
            }

            return View();
        }
    }
}
