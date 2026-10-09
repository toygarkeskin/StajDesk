
using Microsoft.AspNetCore.Mvc;
using StajDesk.Api.Models;
using StajDesk.Api.Services;
using StajDesk.Api.Dtos;

namespace StajDesk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _service;

    public TicketsController(ITicketService service)
    {
        _service = service;
    }

    
[HttpGet]
public IActionResult GetAll()
{
    var tickets = _service.GetAll();

    var response = tickets.Select(ticket => new TicketResponse
    {
        Id = ticket.Id,
        Title = ticket.Title,
        Description = ticket.Description,
        IsCompleted = ticket.IsCompleted
    }).ToList();

    return Ok(response);
}

    
[HttpPost]
public IActionResult Create(CreateTicketRequest request)
{
    var ticket = new Ticket
    {
        Title = request.Title,
        Description = request.Description,
        IsCompleted = false
    };

    _service.Add(ticket);

    
var response = new TicketResponse
{
    Id = ticket.Id,
    Title = ticket.Title,
    Description = ticket.Description,
    IsCompleted = ticket.IsCompleted
};

return Created($"/api/Tickets/{ticket.Id}", response);

}

   
[HttpPut("{id}")]
public IActionResult Update(int id, UpdateTicketRequest request)
{
    var updatedTicket = new Ticket
    {
        Id = id,
        Title = request.Title,
        Description = request.Description,
        IsCompleted = request.IsCompleted
    };

    var result = _service.Update(id, updatedTicket);

    if (!result)
    {
        return NotFound();
    }

    var response = new TicketResponse
    {
        Id = updatedTicket.Id,
        Title = updatedTicket.Title,
        Description = updatedTicket.Description,
        IsCompleted = updatedTicket.IsCompleted
    };

    return Ok(response);
}


    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var result = _service.Delete(id);

        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }
}
