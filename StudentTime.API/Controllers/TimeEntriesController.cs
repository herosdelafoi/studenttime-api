using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentTime.Core.DTOs.TimeTracking;
using StudentTime.Core.Exceptions;
using StudentTime.Core.Interfaces;
using System.Security.Claims;

namespace StudentTime.API.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class TimeEntriesController : ControllerBase
{
    private readonly ITimeTrackingService _timeTrackingService;

    public TimeEntriesController(ITimeTrackingService timeTrackingService)
    {
        _timeTrackingService = timeTrackingService;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found in token");
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(TimeEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartEntry([FromBody] StartTimeEntryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = GetUserId();
            var response = await _timeTrackingService.StartEntryAsync(userId, request);
            return CreatedAtAction(nameof(GetEntry), new { id = response.Id }, response);
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(TimeEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEntry([FromBody] CreateTimeEntryRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();
            return BadRequest(new { message = "Données invalides", errors });
        }

        try
        {
            var userId = GetUserId();
            var response = await _timeTrackingService.CreateEntryAsync(userId, request);
            return CreatedAtAction(nameof(GetEntry), new { id = response.Id }, response);
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/stop")]
    [ProducesResponseType(typeof(TimeEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StopEntry(string id)
    {
        try
        {
            var userId = GetUserId();
            var response = await _timeTrackingService.StopEntryAsync(userId, id);
            return Ok(response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(TimeEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetActiveEntry()
    {
        var userId = GetUserId();
        var response = await _timeTrackingService.GetActiveEntryAsync(userId);
        if (response == null)
        {
            return NoContent();
        }
        return Ok(response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TimeEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntries([FromQuery] int page = 1, [FromQuery] int pageSize = 10000)
    {
        // PageSize par défaut élevé pour récupérer toutes les sessions
        // La pagination est gérée côté frontend pour une meilleure UX
        var userId = GetUserId();
        var response = await _timeTrackingService.GetEntriesAsync(userId, page, pageSize);
        return Ok(response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TimeEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry(string id)
    {
        var userId = GetUserId();
        var entries = await _timeTrackingService.GetEntriesAsync(userId, 1, 1000);
        var entry = entries.FirstOrDefault(e => e.Id == id);
        if (entry == null)
        {
            return NotFound(new { message = "Session introuvable" });
        }
        return Ok(entry);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TimeEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEntry(string id, [FromBody] UpdateTimeEntryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = GetUserId();
            var response = await _timeTrackingService.UpdateEntryAsync(userId, id, request);
            return Ok(response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntry(string id)
    {
        try
        {
            var userId = GetUserId();
            await _timeTrackingService.DeleteEntryAsync(userId, id);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(TimeEntryStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        var response = await _timeTrackingService.GetStatsAsync(userId, startDate, endDate);
        return Ok(response);
    }
}

