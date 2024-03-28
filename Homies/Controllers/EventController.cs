using Homies.Data;
using Homies.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Globalization;
using System.Security.Claims;

namespace Homies.Controllers
{
	[Authorize]
	public class EventController : Controller
	{
		private readonly HomiesDbContext data;

		public EventController(HomiesDbContext context)
		{
			data = context;
		}

		public async Task<IActionResult> All()
		{
			var events = await data.Events
				.AsNoTracking()
				.Select(e => new EventInfoViewModel(
					e.Id,
					e.Name,
					e.Start,
					e.Type.Name,
					e.Organiser.UserName))
				.ToListAsync();

			return View(events);
		}


		[HttpPost]
		public async Task<IActionResult> Join(int id)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);


			var userIsParticipant = await data.EventsParticipants
				.AnyAsync(ep => ep.EventId == id && ep.HelperId == userId);

			if (userIsParticipant)
			{
				return RedirectToAction("Joined");
			}

			var eventParticipant = new EventParticipant
			{
				EventId = id,
				HelperId = userId
			};

			await data.EventsParticipants.AddAsync(eventParticipant);
			await data.SaveChangesAsync();

			return RedirectToAction("Joined");
		}


		[HttpGet]
		public async Task<IActionResult> Joined()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

			var events = await data.EventsParticipants
				.Where(ep => ep.HelperId == userId)
				.AsNoTracking()
				.Select(ep => new EventInfoViewModel(
					ep.Event.Id,
					ep.Event.Name,
					ep.Event.Start,
					ep.Event.Type.Name,
					ep.Event.Organiser.UserName))
				.ToListAsync();

			return View(events);
		}

		[HttpPost]
		public async Task<IActionResult> Leave(int id)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

			var eventParticipant = await data.EventsParticipants
				.FirstOrDefaultAsync(ep => ep.EventId == id && ep.HelperId == userId);

			if (eventParticipant == null)
			{
				return RedirectToAction("Joined");
			}

			data.EventsParticipants.Remove(eventParticipant);
			await data.SaveChangesAsync();

			return RedirectToAction("All");
		}

		[HttpGet]
		public async Task<IActionResult> Add()
		{
			var model = new EventFormViewModel();

			model.Types = await GetTypes();

			return View(model);
		}

		[HttpPost]
		public async Task<IActionResult> Add(EventFormViewModel model)
		{
			DateTime start = DateTime.Now;
			DateTime end = DateTime.Now;

			if(!DateTime.TryParseExact(model.Start,
				DataConstants.DateFormat,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out start))
			{
				ModelState.AddModelError(nameof(model.Start),
					$"Invalid date format. Format must be: {DataConstants.DateFormat}");
			}

			if (!DateTime.TryParseExact(model.End,
				DataConstants.DateFormat,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out end))
			{
				ModelState.AddModelError(nameof(model.End),
					$"Invalid date format. Format must be: {DataConstants.DateFormat}");
			}

			if (!ModelState.IsValid)
			{
				model.Types = await GetTypes();

				return View(model);
			}

			var entity = new Event
			{
				Name = model.Name,
				Description = model.Description,
				OrganiserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
				CreatedOn = DateTime.Now,
				Start = start,
				End = end,
				TypeId = model.TypeId
			};

			await data.Events.AddAsync(entity);
			await data.SaveChangesAsync();

			return RedirectToAction("All");
		}


		public async Task<IEnumerable<TypeViewModel>> GetTypes()
		{
			return await data.Types
				.AsNoTracking()
				.Select(t => new TypeViewModel
				{
					Id = t.Id,
					Name = t.Name
				})
				.ToListAsync();
		}


		[HttpGet]
		public async Task<IActionResult> Edit(int id)
		{
			var e = await data.Events.FindAsync(id);

			if(e == null)
			{
				return BadRequest();
			}

			if(e.OrganiserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
			{
				return Unauthorized();
			}

			var model = new EventFormViewModel
			{
				Name = e.Name,
				Description = e.Description,
				Start = e.Start.ToString(DataConstants.DateFormat),
				End = e.End.ToString(DataConstants.DateFormat),
				TypeId = e.TypeId,
				Types = await GetTypes()
			};

			return View(model);
		}

		[HttpPost]
		public async Task<IActionResult> Edit(EventFormViewModel model, int id)
		{
			var e = await data.Events.FindAsync(id);

			if (e == null)
			{
				return BadRequest();
			}

			if (e.OrganiserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
			{
				return Unauthorized();
			}

			DateTime start = DateTime.Now;
			DateTime end = DateTime.Now;

			if (!DateTime.TryParseExact(model.Start,
				DataConstants.DateFormat,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out start))
			{
				ModelState.AddModelError(nameof(model.Start),
					$"Invalid date format. Format must be: {DataConstants.DateFormat}");
			}

			if (!DateTime.TryParseExact(model.End,
				DataConstants.DateFormat,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out end))
			{
				ModelState.AddModelError(nameof(model.End),
					$"Invalid date format. Format must be: {DataConstants.DateFormat}");
			}

			if(!ModelState.IsValid)
			{
				model.Types = await GetTypes();

				return View(model);
			}

			e.Start = start;
			e.End = end;	
			e.Description = model.Description;
			e.Name = model.Name;
			e.TypeId = model.TypeId;

			await data.SaveChangesAsync();

			return RedirectToAction("All");
		}

		public async Task<IActionResult> Details(int id)
		{
			var model = await data.Events
				.Where(e => e.Id == id)
				.AsNoTracking()
				.Select(data => new EventDetailsViewModel
				{
					Id = data.Id,
					Name = data.Name,
					Description = data.Description,
					Start = data.Start.ToString(DataConstants.DateFormat),
					End = data.End.ToString(DataConstants.DateFormat),
					Type = data.Type.Name,
					Organiser = data.Organiser.UserName,
					CreatedOn = data.CreatedOn.ToString(DataConstants.DateFormat)
				}).FirstOrDefaultAsync();

			if(model == null)
			{
				return BadRequest();
			}	

			return View(model);
		}
	}
}
