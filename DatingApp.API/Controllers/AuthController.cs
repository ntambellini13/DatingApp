using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DatingApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IMapper mapper;
        private readonly SignInManager<User> signInManager;
        private readonly UserManager<User> userManager;

        public AuthController(IConfiguration config, IMapper mapper,
        UserManager<User> userManager, SignInManager<User> signInManager)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.config = config;
            this.mapper = mapper;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserForRegisterDto userForRegisterDto)
        {
            var userToCreate = this.mapper.Map<User>(userForRegisterDto);

            var result = await this.userManager.CreateAsync(userToCreate, userForRegisterDto.Password);

            var userToReturn = this.mapper.Map<UserForDetailedDto>(userToCreate);

            if (result.Succeeded)
            {
                return CreatedAtRoute("GetUser", new
                {
                    controller = "Users",
                    id = userToCreate.Id
                }, userToReturn);
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserForLoginDto userForLoginDto)
        {
            var user = await this.userManager.FindByNameAsync(userForLoginDto.Username);

            var result = await this.signInManager.CheckPasswordSignInAsync(user, userForLoginDto.Password, false);
            
            if (result.Succeeded)
            {
                var appUser = await userManager.Users.Include(p => p.Photos)
                    .FirstOrDefaultAsync(u => u.NormalizedUserName == userForLoginDto.Username.ToUpper());
                
                var userToReturn = this.mapper.Map<UserForListDto>(appUser);

                return Ok(new
                {
                    token = GenerateJwtToken(appUser).Result,
                    user = userToReturn
                });
            }

            return Unauthorized();
        }

        private async Task<string> GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName)
            };

            var roles = await this.userManager.GetRolesAsync(user);

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.config.GetSection("AppSettings:Token").Value));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}