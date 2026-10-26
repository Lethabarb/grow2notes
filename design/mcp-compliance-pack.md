# MCP compliance pack

This pack is for the provider's lawyer or privacy officer. It covers Grow2Notes' admin MCP server, designed in
`mcp-server.md` beside this file. MCP (Model Context Protocol) lets an AI assistant look up and change records in
another system. Through it, the provider's managers and the operator's Support person can use an approved AI
assistant to update Grow2Notes' setup: staff accounts, participants' names, dates of birth and goals, and the
checklists and prompts workers use. Workers never use an AI assistant in Grow2Notes, and no AI assistant ever
receives progress notes. A section number marked § refers to `mcp-server.md` unless another file is named.

Words used here:

- **The provider**: the organisation that provides supports to participants and keeps its progress notes in
  Grow2Notes.
- **Workers**: the provider's support workers, who write the progress notes.
- **The operator**: the business that hosts and runs Grow2Notes for the provider under a written hosting agreement.
- **Support person**: the operator's IT support person, who holds a Support account in Grow2Notes to make the admin
  changes the provider asks for. A Support account is not a worker's account.
- **AI provider**: the company behind an AI assistant, which receives what is sent through the assistant.

The pack has three parts:

1. the operator's check of its own AI provider (§7.1);
2. draft privacy policy, collection notice and staff notice wording (§7.4);
3. draft amendments to the hosting agreement (§7.5).

They let the provider decide whether to approve the operator's AI assistant, and let its lawyer or privacy officer
start on the wording while the server is built. The provider's own check of the AI providers its managers use, its
approval of each AI assistant, its one-page decision, the final wording, publishing the notices and signing the
amendments are steps for the provider and the operator before the server is switched on (§11.1), and are not part of
this pack. Nor is repeating the check (§11.5).

Every field is in square brackets, and everything in square brackets is a field. Fields are filled in only on a copy
of this pack kept outside this public repository, never here.

Sent to the provider: [date sent]

## Fields

- `[operator's AI provider]` and `[account type]`: the AI provider and the type of business account the Support
  person uses. The operator fills them in.
- `[answer]`, `[date checked]` and `[link]`: one of each for every check in part 1. The operator fills them in from
  the AI provider's current terms for that account type.
- `[AI providers]`: every AI provider used for admin, and who uses it, for example: Provider A, used by our managers,
  and Provider B, used by our IT operator. The operator fills in its own AI provider before it sends the pack. The AI
  providers the managers use are added once the provider has approved their assistants.
- `[countries]`: where those AI providers may store and process the information, from each one's check of where data
  is processed and stored (check 3 in part 1, for the operator's).
- `[privacy contact]`: who to contact with privacy questions. The provider fills it in.
- `[Support account holders]`: the people at the operator who hold Support accounts. The operator fills it in.
- `[date sent]`: the date the operator sends the pack to the provider.

## 1. The operator's AI provider checked

AI provider: [operator's AI provider]. Account type: [account type].

The provider uses this check to decide whether to approve the operator's AI assistant for the Support person, and in
its one-page decision. Each answer comes from the AI provider's current terms for that account type.

1. Which account types come with a data processing agreement that makes the AI provider a processor acting on the
   customer's instructions, and what that agreement promises: breach notice time, deletion on termination, notice of
   new sub-processors
   - Answer: [answer]
   - Date checked: [date checked]
   - Link: [link]
2. Whether chats and tool results are used to train models, by default, on that account type
   - Answer: [answer]
   - Date checked: [date checked]
   - Link: [link]
3. Where data is processed and stored, and whether an Australian option exists for that account type
   - Answer: [answer]
   - Date checked: [date checked]
   - Link: [link]
4. How long chats are kept, including any safety or abuse-review retention the AI provider decides itself, and what
   deleting a chat removes
   - Answer: [answer]
   - Date checked: [date checked]
   - Link: [link]
5. Memory features that carry details from one chat into other chats, and whether an administrator can turn them off
   - Answer: [answer]
   - Date checked: [date checked]
   - Link: [link]
6. Whether an administrator can require approval before tool calls, turn off unattended or scheduled agents, and turn
   off other tools such as web search for these chats
   - Answer: [answer]
   - Date checked: [date checked]
   - Link: [link]
7. The price of a business account
   - Answer: [answer]
   - Date checked: [date checked]
   - Link: [link]

The check is repeated when an AI provider's terms change and at least quarterly (§11.5).

## 2. Privacy policy, collection notice and staff notice

Add to the privacy policy, beside the hosting statement already planned (`design.md` §12, HPP 9 row):

> Our managers and our IT operator sometimes use AI assistants to update the setup of our progress-notes system:
> staff accounts, participants' names, dates of birth and goals, and the checklists and prompts our workers use. When
> they do, the details they look up or change, which can include a participant's name, date of birth and goals, are
> sent to the company that provides the AI assistant. The AI providers we and our IT operator use for this are
> [AI providers]. They may store and process the information in [countries]. They handle it under contracts that
> limit what they can do with it. They never receive progress notes. If you have questions, contact
> [privacy contact]. If you would rather we did not use AI assistants for your details, tell us: our managers will
> make changes to your details in the system itself, although your name may still appear when staff search for a
> similar name.

The collection notice gains one sentence to the same effect:

> Names, dates of birth and goals may be disclosed to the AI providers used for record administration,
> [AI providers], in [countries].

The staff privacy notice or handbook gains one sentence:

> Staff names and work email addresses may be disclosed to the AI providers used for administration, [AI providers],
> in [countries], when managers or our IT operator use AI assistants to administer staff accounts.

When the approved list changes, these are updated before the operator registers the new assistant (§11.2).

## 3. Hosting agreement amendments

These amend the written hosting agreement between the provider and the operator. The provider's lawyer places each
one and settles its clause numbers and defined terms.

1. **The MCP server.** The operated service includes the MCP server and its authorisation server, hosted in Melbourne
   like the rest of the service.
2. **AI providers used for admin.** The provider approves each AI assistant that may connect to Grow2Notes. The
   operator registers only those assistants, only at the provider's written request, and removes one within one
   business day of being asked. The AI providers the provider's own managers use are the provider's recipients, under
   the provider's own contracts with them, not the operator's sub-processors.
3. **The operator's own AI provider.** The Support person connects only from the operator's own business account
   ([account type]) with [operator's AI provider], using an approved assistant. [operator's AI provider] is the
   operator's sub-processor. The operator keeps a data processing agreement with it covering no training on the
   provider's information, breach notice and deletion. The operator tells the provider before changing AI provider or
   account type, and passes on any breach notice from [operator's AI provider] without delay.
4. **Support accounts.** The operator's Support accounts in Grow2Notes are those held by [Support account holders].
   They act only on the provider's request, and every change they make is audited with a reason. A Support account
   can invite workers. Every worker can read every participant's notes. So a Support account can obtain note access.
   The operator invites only people the provider names, and never an address the operator controls.
5. **Turning the MCP server off.** The provider may ask the operator to turn the MCP server off at any time, and the
   operator does so within one business day.
6. **MCP incidents.** The operator's prompt breach notice to the provider also covers MCP-related incidents, for
   example a suspected stolen token, a wrong change made through an assistant, or a breach notice from
   [operator's AI provider].
