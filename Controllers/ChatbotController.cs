using ChillTour.Models.Chatbot;
using ChillTour.Services.Chatbot;
using Microsoft.AspNetCore.Mvc;

namespace ChillTour.Controllers;

[ApiController]
[Route("[controller]")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpPost("Ask")]
    public async Task<ActionResult<ChatbotResponse>> Ask([FromBody] ChatbotRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ChatbotResponse
            {
                Success = false,
                Message = "Vui lòng nhập nội dung cần tư vấn."
            });
        }

        var answer = await _chatbotService.AskAsync(request.Message, cancellationToken);
        return Ok(new ChatbotResponse
        {
            Success = true,
            Message = answer
        });
    }
}
