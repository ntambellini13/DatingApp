using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DatingApp.API.Controllers
{
    [ServiceFilter(typeof(LogUserActivity))]
    [Route("api/users/{userId}/[controller]")]
    [ApiController]
    public class MessagesController : ControllerBase
    {
        private readonly IDatingRepository repo;
        private readonly IMapper mapper;
        public MessagesController(IDatingRepository repo, IMapper mapper)
        {
            this.mapper = mapper;
            this.repo = repo;
        }

        [HttpGet("{id}", Name = "GetMessage")]
        
        public async Task<IActionResult> GetMessage(int userId, int id)
        {
             if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var messageFromRepo = await this.repo.GetMessage(id);

            if (messageFromRepo == null)
                return NotFound();
            
            return Ok(messageFromRepo);
        }

        [HttpGet]
        public async Task<IActionResult> GetMessagesForUser(int userId, [FromQuery]MessageParams messageParams)
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            messageParams.UserId = userId;

            var messagesFromRepo = await this.repo.GetMessagesForUser(messageParams);

            var messages = this.mapper.Map<IEnumerable<MessageToReturnDto>>(messagesFromRepo);

            Response.AddPagination(messagesFromRepo.CurrentPage, messagesFromRepo.PageSize, messagesFromRepo.TotalCount, messagesFromRepo.TotalPages);

            return Ok(messages);
        }

        [HttpGet("thread/{recipientId}")]
        public async Task<IActionResult> GetMessageThread(int userId, int recipientId)
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var messageFromRepo = await this.repo.GetMessageThread(userId, recipientId);

            var messageThread = this.mapper.Map<IEnumerable<MessageToReturnDto>>(messageFromRepo);

            return Ok(messageThread);
        }

        [HttpPost]
        public async Task<IActionResult> CreateMessage(int userId, MessageForCreationDto messageForCreationDto)
        {
            var sender = await this.repo.GetUser(userId);

             if(sender.Id != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            messageForCreationDto.SenderId = userId;

            var recipient = await this.repo.GetUser(messageForCreationDto.RecipientId);

            if (recipient == null)
                return BadRequest("Could not find user");

            var message = this.mapper.Map<Message>(messageForCreationDto);

            this.repo.Add(message);

            

            if (await this.repo.SaveAll()) {
                var messageToReturn = this.mapper.Map<MessageToReturnDto>(message);
                return CreatedAtRoute("GetMessage", new {id = message.Id}, messageToReturn);
            }
                

            throw new Exception("Creating the message failed on save");
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> DeleteMessage(int id, int userId) 
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();
            
            var messageFromRepo = await this.repo.GetMessage(id);

            if (messageFromRepo.SenderId == userId)
                messageFromRepo.SenderDeleted = true;

            if (messageFromRepo.RecipientId == userId)
                messageFromRepo.RecipientDeleted = true;

            if (messageFromRepo.SenderDeleted && messageFromRepo.RecipientDeleted)
                this.repo.Delete(messageFromRepo);

            if (await this.repo.SaveAll())
                return NoContent();
            
            throw new Exception("Error deleting the message");
        }

        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkMessagesAsRead(int userId, int id)
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var message = await this.repo.GetMessage(id);

            if (message.RecipientId != userId)
                return Unauthorized();

            message.IsRead = true;
            message.DateRead = DateTime.Now;

            await this.repo.SaveAll();

            return NoContent();
        }
    }
}