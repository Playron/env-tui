using ContactExtractor.Api.AI;
using ContactExtractor.Api.Domain;
using ContactExtractor.Api.Domain.ValueObjects;

namespace ContactExtractor.Api.Services;

public static class ContactMergeHelper
{
    public static bool ShouldUseAi(List<Contact> contacts) =>
        contacts.Count < 2 || contacts.Any(c => c.Confidence < 0.5);

    public static List<Contact> Merge(
        Guid sessionId,
        List<Contact> regexContacts,
        LlmExtractionResult llmResult)
    {
        var merged = new List<Contact>(regexContacts);

        foreach (var llm in llmResult.Contacts)
        {
            var isDuplicate = regexContacts.Any(r =>
                (!string.IsNullOrEmpty(r.Email) &&
                 string.Equals(r.Email, llm.Email, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.FullName) &&
                 string.Equals(r.FullName, llm.FullName, StringComparison.OrdinalIgnoreCase)));

            if (isDuplicate) continue;

            var contact = new Contact(sessionId)
            {
                FullName = llm.FullName,
                FirstName = llm.FirstName,
                LastName = llm.LastName,
                Organization = llm.Organization,
                Title = llm.Title,
                Address = llm.Address,
                Confidence = llmResult.OverallConfidence * 0.9,
                ExtractionSource = "ai"
            };
            contact.SetEmail(EmailAddress.TryCreate(llm.Email));
            contact.SetPhone(PhoneNumber.TryCreate(llm.Phone));
            merged.Add(contact);
        }

        return merged;
    }
}
