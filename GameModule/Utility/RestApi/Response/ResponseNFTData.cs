namespace Utility.RestApi.Response
{
    public class ResponseNFTData
    {
        public int tokenId;
        public string contractAddress;
        public int chain;
        public int amount;

        public ResponseNFTData() { }

        public ResponseNFTData(int tokenId, string contractAddress, int chain, int amount)
        {
            this.tokenId = tokenId;
            this.contractAddress = contractAddress;
            this.chain = chain;
            this.amount = amount;
        }
    }
}