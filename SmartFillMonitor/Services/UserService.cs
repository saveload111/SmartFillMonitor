 using SmartFillMonitor.Models;
using SmartFillMonitor.Services.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SmartFillMonitor.Services
{
   public static class UserService
    {//用于密码加盐的固定字符串
        private const string StaticSalt = "MySuperSecretSalt--2026!@#";
        private static User? _currenUser;
        //登录状态发生改变的事件
        public static event Action<User?>? LoginStateChanged;
        public static User? CurrentUser
        {
            get=>_currenUser;
            private set
            {
                if(_currenUser != value)
                {

                    _currenUser = value;
                    LoginStateChanged?.Invoke(_currenUser);
                }

            }

            }


        public static async Task InitializeAsync()
        {
            try
            {
                bool hasUsers = await DbProvider.Fsql.Select<User>().AnyAsync();
                if (!hasUsers)
                {
                    var now = DateTime.Now;
                    var users = new List<User>
                    {
                        new User()
                        {
                            UserName="admin",
                            PasswordHash= HashPassword("admin"),
                            Role=Role.Admin,
                            CreatedAt= now,

                        },
                          new User()
                        {
                            UserName="engineer",
                            PasswordHash= HashPassword("engineer"),
                            Role=Role.Engineer,
                            CreatedAt= now,

                        },



                    };
                    var affrows = await DbProvider.Fsql.Insert(users).ExecuteAffrowsAsync();
                    if (affrows == 2)

                    {
                        LogService.Info("系统初始化：创建默认用户成功");
                    }
                    else
                    {
                        throw new Exception("创建失败");

                    }
                }

            }
            catch (Exception ex)
            {

                LogService.Error("系统初始化用户失败",ex);
            }

        }

        public static async Task<bool> AuthenticateAsync(string username, string password)
        {
            if(string.IsNullOrEmpty(username)|| string.IsNullOrEmpty(password)) return false;
            try
            {
                var user = await DbProvider.Fsql.Select<User>()
                    .Where(u => u.UserName == username)
                    .FirstAsync();
                if (user == null) return false;
                var inputHash = HashPassword(password);
                bool isValid = string.Equals(inputHash, user.PasswordHash, StringComparison.Ordinal);
                if (isValid)
                {
                    CurrentUser = user;
                    LogService.Info($"用户登录成功：{username}");
                }
                else
                {
                    LogService.Warn($"用户{username}尝试登录失败，密码错误");
                   
                }
                return isValid;
            }
            catch (Exception ex)
            {
                LogService.Error("用户登录验证失败", ex);
               return false; 
            }
       
        
        
        
        
        
        }

        public static  Task LoginoutAsync()
        {
            if(CurrentUser != null) 
                {
                LogService.Info($"用户{CurrentUser.UserName}登出");
                CurrentUser=null;
                }
            return Task.CompletedTask;

        }
        //创建新用户并且保存到数据库
        public static async Task CreateUserAsync(string username, string password,Role role,string displayName)
        {
            if(string.IsNullOrEmpty(username)||string.IsNullOrEmpty(password))
            {
                  throw new ArgumentNullException("用户名和密码不能为空");
            }
            //检查用户名是否已经存在
            bool exists = await DbProvider.Fsql.Select<User>()
                .Where (u => u.UserName == username)
                .AnyAsync();
            if(exists )
            {
                throw new InvalidOperationException($"用户{username}已存在");
              }
            var user = new User
            {

                UserName = username,
                PasswordHash = HashPassword(password),
                Role = role,
                CreatedAt = DateTime.Now,
              
            };
            await DbProvider.Fsql.Insert<User>().ExecuteAffrowsAsync();
            LogService.Info($"创建新用户：{username}");

        }
        //对明文密码进行哈希并且返回十六进制大小写字符串
        public static async Task<List<User>>GetAllUsersAsync()
        {
            try
            {
                return await DbProvider.Fsql.Select<User>()
                    .OrderBy(u => u.UserName)
                    .ToListAsync();


            }
            catch (Exception ex)

            {
                LogService.Error("获取用户列表失败", ex);
                throw;

            }


        }

        private static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            string raw = password + StaticSalt;

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            //优化容量
            var sb = new StringBuilder(bytes.Length*2);
            foreach(var b in bytes)
            {  sb.Append(b.ToString("x2")); }
            return sb.ToString();
        }


    }
}
