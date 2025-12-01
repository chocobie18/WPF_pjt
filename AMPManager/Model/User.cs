namespace AMPManager.Model
{
    public class User
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public int RoleId { get; set; } // 1: 관리자, 2: 일반

        // 관리자 여부 확인용 (화면에서 버튼 숨길 때 사용)
        public bool IsAdmin => RoleId == 1;

        public User(string name, string id, int roleId)
        {
            Name = name;
            Id = id;
            RoleId = roleId;
        }
    }
}