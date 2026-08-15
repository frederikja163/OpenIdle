using System.Linq;
using System.Threading.Tasks;
using Backend.Attributes;
using Backend.Database.Entities;
using Backend.Dtos;
using Backend.Services;

namespace Backend.Controllers;

[SocketController]
public sealed class SkillController(SkillService skillService) : SocketControllerBase
{
    [Request]
    public async Task GetSkills(GetSkillsRequest request)
    {
        Skill[] skills = request.SkillIds is null
            ? await skillService.GetSkillsAsync(ProfileId)
            : await skillService.GetSkillsAsync(ProfileId, request.SkillIds);
        SkillDto[] skillDtos = skills.Select(s => s.ToDto()).ToArray();
        await RespondAsync(new GetSkillsResponse() { Skills = skillDtos });
    }
}
