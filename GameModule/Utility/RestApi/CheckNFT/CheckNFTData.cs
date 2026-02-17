namespace Utility.RestApi
{
    public class CheckNFTData
    {
        public string ownerAddress;
        public string contractAddress;
        public int targetChain;
        public int tokenId;

        public CheckNFTData(string ownerAddress, string contractAddress, int targetChain, int tokenId)
        {
            this.targetChain = targetChain;
            this.contractAddress = contractAddress;
            this.ownerAddress = ownerAddress;
            this.tokenId = tokenId;
        }
    }
}