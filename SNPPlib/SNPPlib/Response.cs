using System;
using System.Linq;

namespace SNPPlib
{
    public enum ResponseCode : short
    {
        Malformed = 0,
        MultiLineResponse = 214,//ends with a 250 response
        SingleLineResponse = 218,
        GatewayReady = 220,
        Goodbye = 221,
        Success = 250,
        BeginInput = 354,
        FatalError = 421,//terminate connection
        NotImplemented = 500,
        DuplicateCommand = 503,
        AdministrativeFailureContinue = 550,
        TechnicalFailureContinue = 554,
        MaximumEntriesExceeded = 552
    }

    public class Response
    {
        public ResponseCode Code { get; private set; }

        public string Message { get; private set; }

        private Response()
        {
        }

        public Response(string response)
        {
            var code = default(ResponseCode);
            if (Enum.TryParse(response.Substring(0, 3), out code) && !Enum.IsDefined(typeof(ResponseCode), code))
                code = ResponseCode.Malformed;
            Code = code;

            var responseLines = response.TrimEnd('\0').Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            for (var i = responseLines.Count - 1; 0 <= i; i--)
            {
                var responseLine = responseLines[i];
                if (Code == ResponseCode.MultiLineResponse && responseLine.StartsWith(String.Format("{0} ", ResponseCode.Success)))
                    responseLines.RemoveAt(i);
                responseLines[i] = responseLine.TrimStart(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' ', });
            }

            Message = String.Join("\r\n", responseLines);
        }

        public static Response FatalResponse(string message = default(string))
        {
            return new Response() { Code = ResponseCode.FatalError, Message = message };
        }
    }
}