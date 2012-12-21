﻿/**
 * © ORConcept 2012
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

using BoxSync.Core;
using BoxSync.Core.Primitives;
using BoxSync.Core.Statuses;
using System.Collections.Specialized;

namespace Core.BoxHelper
{
    public delegate void LoginSuccessfulHandler(User userInfo);
    public delegate void LoginCancelledHandler();
    public delegate void GetFileCompletedAction(BoxSync.Core.Primitives.File file, byte[] data);
    public delegate void GetTicketSuccessfulHandler(string ticket);
    public delegate void GetTicketFailedHandler();

    /// <summary>
    /// This class wraps some of the API provided by BoxSync .NET library and provides some more 
    /// directly using the REST Box API 
    /// </summary>
    public class BoxProvider
    {
        /// <summary>
        /// Internal class for the GetFileContent thread parameters
        /// </summary>
        class GetFileThreadParams
        {
            public BoxSync.Core.Primitives.File File
            {
                get;
                private set;
            }

            public GetFileCompletedAction CompletedAction
            {
                get;
                private set;
            }

            public GetFileThreadParams(BoxSync.Core.Primitives.File file, GetFileCompletedAction completedAction)
            {
                this.File = file;
                this.CompletedAction = completedAction;
            }
        }

        class UploadFileThreadParams
        {
            public string FilePath
            {
                get;
                private set;
            }

            public long FolderID
            {
                get;
                private set;
            }

            public bool Encrypted
            {
                get;
                private set;
            }

            public OperationFinished<UploadFileResponse> OperationFinished
            {
                get;
                private set;
            }

            public UploadFileThreadParams(string filePath, long folderID, bool encrypted, OperationFinished<UploadFileResponse> operationFinished)
            {
                this.FilePath = filePath;
                this.FolderID = folderID;
                this.Encrypted = encrypted;
                this.OperationFinished = operationFinished;
            }
        }

        private enum Method : int
        {
            GET = 0,
            POST
        }

        private const string
            APIV1_DOWNLOAD_FILE = "https://www.box.net/api/1.0/download/{0}/{1}",
            APIV1_UPDATE_TAGS = "https://www.box.net/api/1.0/rest?action=add_to_tag&api_key={0}&auth_token={1}&target={2}&target_id={3}",
            TARGET_FOLDER = "folder",
            TARGET_FILE = "file",
            METHOD_POST = "POST",
            METHOD_GET = "GET";

        public const string ENCRYPTED = "Encrypted";

        public event LoginSuccessfulHandler LoginSuccessful;
        public event LoginCancelledHandler LoginFailed;
        public event LoginCancelledHandler LoginCancelled;
        public event GetTicketSuccessfulHandler GetTicketSucceeded;
        public event GetTicketFailedHandler GetTicketFailed;

        private readonly string[] METHODS = new string[] { METHOD_GET, METHOD_POST };

        private readonly BoxManager _manager;
        private string _ticket;
        private string _authenticationToken;
        private string _apiKey;

        public BoxProvider(string applicationApiKey)
        {
            _manager = new BoxManager(applicationApiKey, "http://box.net/api/soap", null);
            _apiKey = applicationApiKey;
        }

        public BoxProvider(string applicationApiKey, string authenticationToken)
        {
            _manager = new BoxManager(applicationApiKey, "http://box.net/api/soap", null, authenticationToken);
            _apiKey = applicationApiKey;
            _authenticationToken = authenticationToken;
        }

        /// <summary>
        /// Returns the authentication token for that session
        /// </summary>
        public string AuthenticationToken
        {
            get { return _authenticationToken; }
        }

        /// <summary>
        /// Asynchronously gets authorization ticket 
        /// and opens web browser to logging on Box.NET portal
        /// </summary>
        public void StartAuthentication()
        {
            _manager.GetTicket(GetTicketCompleted);
        }

        public void StartAuthenticationEx()
        {
            _manager.GetTicket(GetTicketCompletedEx);
        }


        //public void AuthenticateUser(string userName, string password, OperationFinished<AuthenticateUserResponse> authenticateUserComplete)
        //{
        //    _manager.AuthenticateUser(userName, password, string.Empty, authenticateUserComplete);
        //}

        /// <summary>
        /// Finishes authorization process after user has 
        /// successfully finished loggin process on Box.NET portal
        /// </summary>
        /// <param name="printUserInfoCallback">Callback method which will be invoked after operation completes</param>
        public void FinishAuthentication(Action<User> printUserInfoCallback)
        {
            _manager.GetAuthenticationToken(_ticket, GetAuthenticationTokenCompleted, printUserInfoCallback);
        }

        /// <summary>
        /// Displays the Box Login in a dialog box that embeds a browser
        /// </summary>
        /// <param name="url"></param>
        private void ShowBrowserInDialog(string url)
        {
            LoginForm loginDlg = new LoginForm(url);
            if (loginDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Login is supposed to be sucessful
                if (LoginSuccessful != null)
                {
                    _manager.GetAuthenticationToken(_ticket, GetAuthenticationTokenCompleted);
                }
            }
            else
            {
                // Login has been cancelled
                if (LoginCancelled != null)
                {
                    LoginCancelled();
                }
            }
        }

        /// <summary>
        /// Gets the folder structure. Synchro method
        /// </summary>
        /// <param name="folderID"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public Folder GetFolderStructure(long folderID, RetrieveFolderStructureOptions options)
        {
            Folder folder;

            GetAccountTreeStatus status = _manager.GetFolderStructure(folderID, options, out folder);

            if (status != GetAccountTreeStatus.Successful)
            {
                throw new ApplicationException(string.Format("Failed to retrieve folder tree. FolderID=[{0}], OperationStatus={1}", folderID, status));
            }

            return folder;
        }

        /// <summary>
        /// Gets the files informqtion for a given folder. Synchronous call
        /// </summary>
        /// <param name="folderID"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public Folder GetFiles(long folderID, RetrieveFolderStructureOptions options = RetrieveFolderStructureOptions.OneLevel)
        {
            Folder folder;
            _manager.GetFolderStructure(folderID, options, out folder);

            return folder;
        }

        /// <summary>
        /// Get files information for a given folder asynchronously
        /// </summary>
        /// <param name="folderID"></param>
        /// <param name="operationFinished"></param>
        /// <param name="options"></param>
        public void GetFilesAsync(long folderID, OperationFinished<GetFolderStructureResponse> operationFinished, RetrieveFolderStructureOptions options = RetrieveFolderStructureOptions.OneLevel)
        {
            _manager.GetFolderStructure(folderID, options, operationFinished);
        }

        /// <summary>
        /// Download the content of a file
        /// 
        /// API version 1: https://www.box.net/api/1.0/download/<auth_token>/<file_id>
        /// </summary>
        /// <param name="fileID"></param>
        /// <returns></returns>
        public byte[] GetFileContent(long fileID)
        {
            return ExecuteGetFileContentV1(fileID);
        }

        /// <summary>
        /// Get the content of a file asynchronously
        /// </summary>
        /// <param name="file"></param>
        /// <param name="fileCompleted"></param>
        public void GetFileContentAsync(BoxSync.Core.Primitives.File file, GetFileCompletedAction fileCompleted)
        {
            GetFileThreadParams threadParams = new GetFileThreadParams(file, fileCompleted);
            Thread thread = new Thread(new ParameterizedThreadStart(GetFileContentThreadMethod));
            thread.Start(threadParams);
        }

        /// <summary>
        /// Set the tags for a given file
        /// </summary>
        /// <param name="fileID"></param>
        /// <param name="tags"></param>
        public void SetFileTags(long fileID, string[] tags)
        {
            SetTags(fileID, TARGET_FILE, tags);
        }

        /// <summary>
        /// Set the tags for a given folder
        /// </summary>
        /// <param name="folderID"></param>
        /// <param name="tags"></param>
        public void SetFolderTags(long folderID, string[] tags)
        {
            SetTags(folderID, TARGET_FOLDER, tags);
        }

        /// <summary>
        /// Upload a file and sets a tag value Encrypted if the file is sent encrypted
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="folderID"></param>
        /// <param name="operationFinished"></param>
        public void UploadFile(string filePath, long folderID, bool encrypted, OperationFinished<UploadFileResponse> operationFinished)
        {
            //Thread thread = new Thread(new ParameterizedThreadStart(UploadFileThreadMethod));
            //thread.Start(new UploadFileThreadParams(filePath, folderID, encrypted, operationFinished));
            _manager.AddFile(filePath, folderID, delegate(UploadFileResponse fileResponse)
            {
                // Try to add the Tag (Encrypted)
                if (encrypted)
                {
                    Folder folder = GetFolderStructure(folderID, RetrieveFolderStructureOptions.Simple);
                    BoxSync.Core.Primitives.File file = folder.Files.Where(f => f.Name == Path.GetFileName(filePath)).FirstOrDefault();
                    if (file != null)
                    {
                        SetFileTags(file.ID, new string[] { ENCRYPTED });
                    }
                }

                if (operationFinished != null)
                {
                    operationFinished(fileResponse);
                }
            });
        }

        /// <summary>
        /// This method has to be called to perform a first login to the Box cloud.
        /// 
        /// It loads the login page and then build the POST to submit the form.
        /// If successful it returns true and the authentication process can continue.
        /// If it fails, it returns false and the requestToken string contains the Request_Token 
        /// to POST the next submit directly
        /// </summary>
        /// <param name="ticket">Box ticket</param>
        /// <param name="userName">User name</param>
        /// <param name="password">Password</param>
        /// <param name="requestToken">Contains the next request token</param>
        /// <returns>true if successful, false otherwise</returns>
        public bool AuthenticateUser(string ticket, string userName, string password, out string requestToken)
        {
            bool authSuccess = false;
            requestToken = string.Empty;
            string url = string.Format("http://www.box.net/api/1.0/auth/{0}", ticket);

            // Use a WebRequest to process the login
            Stream respStream = ExecuteREST_Request(url, Method.GET);
            StreamReader reader = new StreamReader(respStream);

            string responseText = reader.ReadToEnd();

            requestToken = ExtractRequestToken(responseText);

            using (WebClient webClient = new WebClient())
            {
                try
                {
                    NameValueCollection formFields = new NameValueCollection();
                    formFields.Add("login", userName);
                    formFields.Add("password", password);
                    formFields.Add("_pw_sql", "");
                    formFields.Add("remember_login", "on");
                    formFields.Add("__login", "1");
                    formFields.Add("dologin", "1");
                    formFields.Add("reg_step", "");
                    formFields.Add("submit1", "1");
                    formFields.Add("folder", "");
                    formFields.Add("skip_framework_login", "1");
                    formFields.Add("login_or_register_mode", "login");
                    formFields.Add("new_login_or_register_mode", "");
                    formFields.Add("request_token", requestToken);

                    webClient.Proxy = null;
                    string actionUrl = string.Format("https://www.box.net/api/1.0/auth/{0}", ticket);
                    byte[] result = webClient.UploadValues(actionUrl, METHOD_POST, formFields);
                    string htmlText = ASCIIEncoding.ASCII.GetString(result);

                    if (CheckAuthenticated(htmlText))
                    {
                        authSuccess = true;
                    }
                    else
                    {
                        requestToken = ExtractRequestToken(htmlText);
                        authSuccess = false;
                    }
                }
                catch (WebException ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            }

            return authSuccess;
        }

        /// <summary>
        /// This method must be used after a call to AuthenticateUser failed. 
        /// 
        /// It may be called many times with the RequestToken returned in the parameter of the same name,
        /// as long as the credentials are wrongly entered
        /// </summary>
        /// <param name="ticket"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <param name="requestToken"></param>
        /// <returns></returns>
        public bool AuthenticateUserEx(string ticket, string userName, string password, ref string requestToken)
        {
            bool authSuccess = false;

            using (WebClient webClient = new WebClient())
            {
                try
                {
                    NameValueCollection formFields = new NameValueCollection();
                    formFields.Add("login", userName);
                    formFields.Add("password", password);
                    formFields.Add("_pw_sql", "");
                    formFields.Add("remember_login", "on");
                    formFields.Add("__login", "1");
                    formFields.Add("dologin", "1");
                    formFields.Add("reg_step", "");
                    formFields.Add("submit1", "1");
                    formFields.Add("folder", "");
                    formFields.Add("skip_framework_login", "1");
                    formFields.Add("login_or_register_mode", "login");
                    formFields.Add("new_login_or_register_mode", "");
                    formFields.Add("request_token", requestToken);

                    webClient.Proxy = null;
                    string actionUrl = string.Format("https://www.box.net/api/1.0/auth/{0}", ticket);
                    byte[] result = webClient.UploadValues(actionUrl, METHOD_POST, formFields);
                    string htmlText = ASCIIEncoding.ASCII.GetString(result);

                    if (CheckAuthenticated(htmlText))
                    {
                        authSuccess = true;
                    }
                    else
                    {
                        requestToken = ExtractRequestToken(htmlText);
                        authSuccess = false;
                    }
                }
                catch (WebException ex)
                {
                    Trace.WriteLine(ex.Message);
                }
            }

            return authSuccess;
        }

        #region Private methods

        /// <summary>
        /// Set the tags of a folder
        /// 
        /// API V1: https://www.box.net/api/1.0/rest?action=add_to_tag&api_key=[API_KEY]&auth_token=[AUTH_TOK]&tags[]=test_tag&tags[]=test_tag1&target=[file | folder]&target_id=[ID]
        //
        /// </summary>
        /// <param name="folderID"></param>
        /// <param name="tags"></param>
        private void SetTags(long ID, string target, string[] tags)
        {
            StringBuilder urlUpdateTags = new StringBuilder(string.Format(APIV1_UPDATE_TAGS, _apiKey, _authenticationToken, target, ID.ToString()));

            foreach (string tag in tags)
            {
                urlUpdateTags.AppendFormat("&tags[]={0}", tag);
            }

            Stream respStream = ExecuteREST_Request(urlUpdateTags.ToString(), Method.POST);
            StreamReader reader = new StreamReader(respStream);

            string responseText = reader.ReadToEnd();
            Trace.WriteLine(responseText);
        }


        private void GetFileContentThreadMethod(object param)
        {
            GetFileThreadParams threadParams = param as GetFileThreadParams;

            byte[] fileData = ExecuteGetFileContentV1(threadParams.File.ID);

            if (threadParams.CompletedAction != null)
            {
                threadParams.CompletedAction(threadParams.File, fileData);
            }
        }

        /// <summary>
        /// Executes a GET REST request and returns the response stream
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private Stream ExecuteREST_Request(string request, Method method)
        {
            WebRequest wrGETURL = WebRequest.Create(request);
            wrGETURL.Method = METHODS[(int)method];
            wrGETURL.Proxy = WebProxy.GetDefaultProxy();

            return wrGETURL.GetResponse().GetResponseStream();
        }

        private byte[] ExecuteGetFileContentV1(long fileID)
        {
            List<byte> data = new List<byte>();

            Stream respStream = ExecuteREST_Request(string.Format(APIV1_DOWNLOAD_FILE, _authenticationToken, fileID), Method.GET);

            int bData;
            do
            {
                bData = respStream.ReadByte();
                if (bData != -1)
                {
                    data.Add((byte)bData);
                }
            }
            while (bData != -1);

            return data.ToArray();
        }

        private void GetAuthenticationTokenCompleted(GetAuthenticationTokenResponse response)
        {
            if (response.Status == GetAuthenticationTokenStatus.Successful)
            {
                //Action<User> printUserInfoCallback = (Action<User>)response.UserState;

                //printUserInfoCallback(response.AuthenticatedUser);
                if (LoginSuccessful != null)
                {
                    _authenticationToken = response.AuthenticationToken;
                    LoginSuccessful(response.AuthenticatedUser);
                }
            }
            else
            {
                if (LoginFailed != null)
                {
                    LoginFailed();
                }
            }
        }

        private void GetTicketCompleted(GetTicketResponse response)
        {
            if (response.Status == GetTicketStatus.Successful)
            {
                _ticket = response.Ticket;

                string url = string.Format("http://www.box.net/api/1.0/auth/{0}", response.Ticket);

                //BrowserLauncher.OpenUrl(url);
                ShowBrowserInDialog(url);
            }
        }

        private void GetTicketCompletedEx(GetTicketResponse response)
        {
            if (response.Status == GetTicketStatus.Successful)
            {
                _ticket = response.Ticket;

                if (GetTicketSucceeded != null)
                {
                    GetTicketSucceeded(response.Ticket);
                }
            }
            else
            {
                if (GetTicketFailed != null)
                {
                    GetTicketFailed();
                }
            }
        }

        private bool CheckAuthenticated(string htmlText)
        {
            return htmlText.IndexOf("api_auth_success") != -1;
        }

        private string ExtractRequestToken(string responseText)
        {
            string requestToken = string.Empty; ;
            
            int idx = responseText.IndexOf("request_token");
            if (idx != -1)
            {
                int startIdx = responseText.IndexOf('\'', idx);
                int endIdx = responseText.IndexOf('\'', startIdx + 1);

                requestToken = responseText.Substring(startIdx + 1, endIdx - startIdx - 1);
            }

            return requestToken;
        }

        #endregion
    }
}
