namespace GameModule.Authentication
{
    /// <summary>
    /// Deserializes the response payload mapping incoming API tokens securely.
    /// </summary>
    public class TokenExchangeResponse
    {
        /// <summary>Expected mapped token key literal format string.</summary>
        public string accessToken;
    }
}
