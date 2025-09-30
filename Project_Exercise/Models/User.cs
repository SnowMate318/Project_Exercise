using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project_Exercise.Models
{
    public class User
    {
        public string Name { get; set; }
        public long Age { get; set; }
    }

    // UserRepository: User 데이터를 Dictionary로 관리
    public class UserRepository
    {
        private readonly Dictionary<string, User> _users = new Dictionary<string, User>();

        // 저장 (이름을 Key로 사용)
        public void Save(User user)
        {
            _users[user.Name] = user;
        }

        // 조회   
        public User Get(string name)
        {
            
            return _users.TryGetValue(name, out var user) ? user : null;
        }

        // 전체 조회
        public IEnumerable<User> GetAll()
        {
            return _users.Values;
        }
    }
}
