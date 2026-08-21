using System.Linq;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class ItemController(ItemService itemService) : SocketControllerBase
{
    [Request]
    public async Task GetItems(GetItemsRequest request)
    {
        Item[] items = request.ItemIds is null
            ? await itemService.GetItemsAsync(ProfileId)
            : await itemService.GetItemsAsync(ProfileId, request.ItemIds);
        ItemDto[] itemDtos = items.Select(i => i.ToDto()).ToArray();
        await RespondAsync(new GetItemsResponse() { Items = itemDtos });
    }
}
