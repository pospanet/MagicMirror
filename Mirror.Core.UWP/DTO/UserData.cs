namespace Pospa.Mirror.Common.DTO
{
    public class UserData
    {
        public string UserId { get; set; }

        public string AccessToken { get; set; }


        public UserData() { }

        public UserData(string userId, string accessToken)
        {
            this.UserId = userId;
            this.AccessToken = accessToken;
        }
    }
}
