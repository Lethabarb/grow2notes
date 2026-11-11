# Word-template set-up list

What a manager enters so that Grow2Notes offers the provider's Word support-note template: the common item groups and
their items, in order. It is used by the managers' production set-up
([F06.05](../design/backlog/E06-hardening-and-go-live/F06.05-production-set-up.md)) and by the `/onboard-provider`
skill ([onboard-provider.md](onboard-provider.md)), whose `template.csv` takes each item as a "Common item" row, or a
"Common item with details" row for an item marked **needs detail** below. It follows D104 and the assumed defaults A145
to A147 (design.md §13).

## What this file holds, and what it leaves out

- **Only the template's fixed wording**, taken from the provider's five Word days (Monday 14 to Friday 18 September
  2026) and checked against every participant's block in them. It never holds a participant's name, a goal, a
  Medication line or a note.
- **Word for word, with three tidy-ups:** a full stop at the end of a line is left off, the spacing around slashes and
  dashes is evened out, and an unclosed bracket or quotation mark is closed. Where the days differ, the wording most of
  them use is given, and a slip on one day (a stray letter before a heading, "taking" for "talking") is not copied.
- **Names are placeholders**, because this repository is public: `<base venue>` (the program's base location),
  `<bus name>`, `<venue>`, the named places (`<pool>`, `<gym>`, `<bakehouse>`, `<church>`, `<sanctuary>`,
  `<men’s shed>`, `<pizza shop>`, `<woodwork site>`, `<market square>`) and `<instructor>`. Enter the provider's own
  name for each, as the Word template has it.
- **Not set up:** the frequency choice (occasionally, regularly, continuously), which D104 did not choose; the
  placement-specific and participant-specific variants of a section (R39), such as the shorter Morning Tea and Lunch
  list used on café work days and lines written for one participant; and the wording variants of single days (for
  example "No prompts required" in place of "No prompting required" in the bus travel group). The template holds one
  version of each item.
- **Not items:** Not In, Late Notice and Sick are the attendance boxes on every note (A153); the **Medication** line is
  a switch on each participant (a manager's choice, not part of this list); the goals are each participant's own; and
  **Daily Support Notes** is the Guided notes box. None appears below.

## The rules this list follows

1. Each Word section is one **group**, named with its Word heading. Bracketed purpose tags stay in the name (A145).
2. A sub-heading is its own group, named `<section>: <sub-heading>`, with the purpose tags at the end (A145).
3. A parent line with child options is one item for each option, worded `<parent>: <option>` in the template's own
   words, for example "Assistance required when using: microwave" (A146). A line whose only options are occasionally,
   regularly and continuously is one item, without them (D104 did not choose frequency levels).
4. A travel section names its venue in the group name. The pick-up and drop-off options and the 11 Further Supports
   are entered like any other items (A147).
5. Group order is organisation-wide: enter the groups in the order below, and the items in the order given (A147).
6. An item marked **needs detail** asks for a short text when it is ticked on a note, required to submit (A151).
7. The names and items are at most 200 characters each; every one below is within that.

The full list is 32 groups and 431 items, besides the four pick-up and drop-off lines. The daily report prints only the
Every note group and the groups picked on each note (D106), as the Word documents do, so the provider can enter every
group it uses. The writer picks the groups that applied that day, as staff kept the day's sections in the Word
documents, and a group not picked does not appear on that participant's page (first-use step 225).

## The groups, in order

The Word days do not share one order of sections. Each participant's block follows that day's timetable: travel
first, Activities of choice and Further Supports Provided last, and Morning Tea and Lunch between the morning and the
afternoon activities. Grow2Notes has one order for the whole organisation (A147), so this list puts the groups that
most days use first (travel, then Morning Tea and Lunch), then the activities in the order they first appear in the
week, Monday to Friday, then Activities of choice and Further Supports Provided. A manager can change the order in the
app if the provider prefers another.

### Pick-up and drop-off: the Every note group

The template puts these four lines at the top of every participant's block, with no heading. That is what the
built-in Every note group does (A43): it is on every note and prints first. Enter them in Every note, in this order:

1. Collected from/dropped home by CSW
2. Dropped/picked up at `<base venue>` by parent/external support/family friend/other
3. Collected from/dropped to station
4. Other (staff to complete if different from above) (**needs detail**)

### 1. `<bus name>` Bus Travel (Goal Focused Support, Life Skills Development)

1. No prompting required
2. Reminded to use the bathroom if needed before we leave
3. Prompted seatbelts
4. Reminded of shared space etiquette
5. Reminded of respectful interactions and using manners
6. Reminded of the bus routine – fill in from the back/get off from the front
7. Physical support to get into/out of a vehicle (`<bus name>` bus or CSW vehicle)

### 2. Private vehicle travel to/from `<venue>` (Goal Focused Support, Life Skills Development)

The Word days have this list for two venues: enter one group for each venue.

1. No prompting required
2. Prompted to put bag in the boot
3. Prompted seat belt safety
4. Reminded to treat CSW’s car with care and respect — try not to slam the doors or accidentally scrape bags or gear
   against the exterior
5. Physical support to get into/out of a vehicle (`<bus name>` bus or CSW vehicle)

### 3. Private vehicle OR `<bus name>` bus to/from `<venue>` (Goal Focused Support, Life Skills Development)

1. No prompts required
2. Prompted to put bag in the boot of a private vehicle/safely in the bus
3. Prompted seat belt safety
4. Reminded to treat vehicle with care and respect — try not to slam the doors or accidentally scrape bags or gear
   against the exterior
5. Physical support to get into/out of a vehicle (`<bus name>` bus or CSW vehicle)

### 4. Bus travel from `<venue>` (Goal Focused Support, Life Skills Development)

1. No prompts required
2. Prompted to put bag in the boot
3. Prompted seat belt safety
4. Physical support to get into/out of a vehicle (`<bus name>` bus or CSW vehicle)

### 5. Private vehicle to `<venue>`

No purpose tags. Its items are the bus travel items, with "No prompts required" first and no vehicle item.

1. No prompts required
2. Reminded to use the bathroom if needed before we leave
3. Prompted seatbelts
4. Reminded of shared space etiquette
5. Reminded of respectful interactions and using manners
6. Reminded of the bus routine – fill in from the back/get off from the front

### 6. Morning Tea and Lunch (Goal Focused Support, Life Skills Development)

The template's first line reads "`<base venue>` AND" ("OR" on some days); the joining word is left off, as each line is
its own tick box.

1. `<base venue>`
2. Out in the Community
3. Reminded of hand hygiene
4. Assistance required when using: microwave
5. Assistance required when using: kettle
6. Assistance required carrying hot food back to the table
7. Shadowed/Supported at counter in case assistance is required
8. Money handling support to make lunch purchase
9. Prompted clear communication when talking to a cashier
10. Prompted sitting with the group
11. Prompted to pack up any morning tea/lunch items
12. Prompted to dispose of own rubbish
13. Reminded of appropriate behaviour whilst out in the community
14. Buying lunch in the community, suggested: healthy options
15. Buying lunch in the community, suggested: Halaal options
16. Buying lunch in the community, suggested: sugar free options
17. Buying lunch in the community, suggested: milk free options
18. Buying lunch in the community, reminded: only 1 meal/drink
19. Buying lunch in the community, reminded: no overspending
20. Buying lunch in the community, reminded: only 1 energy drink a day

### 7. Walk and Outdoor Gym: Fitness Park (Goal Focused Support, Physical Activity)

"Walk and Outdoor Gym" is the section and "Fitness Park" its tagged sub-heading. The section has no items of its own, so
it is one group.

1. Prompted to keep to the left to allow others to pass
2. Prompted shared space etiquette
3. Reminded to stick together as a group
4. Reminded to be respectful of others when walking
5. Reminded of safety around animals, to ask permission to approach and pat other walkers’ dogs
6. Reminded to ensure they have all required equipment before attending the outdoor gym. (e.g. drink bottle, towel, hat,
   sunscreen)
7. Prompted to store personal belongings safely in a designated area
8. Prompted to leave personal belongings in the bus
9. Prompted to complete stretches prior to commencing activity
10. Reminded to complete a suitable warm-up (e.g. light walk, jog, mobility exercises)
11. Physical demonstration provided for correct technique on outdoor equipment as required
12. Reminded of proper technique when using equipment to minimise risk of injury
13. Prompted awareness of surroundings and shared space etiquette (e.g. other users, children, cyclists, walkers)
14. Reminded to wipe down equipment where possible or maintain hygiene practices (e.g. use of towel/hand sanitiser)
15. Prompted to remain sun-safe throughout the session (e.g. reapply sunscreen, wear hat, seek shade when needed)
16. Reminded to stay hydrated and take breaks as required
17. Prompted to safely navigate uneven surfaces and outdoor terrain
18. Reminded to ensure all belongings are collected before leaving
19. Reminded of appropriate behaviour whilst out in the community

### 8. Swimming @ `<pool>` (Goal Focused Support, Physical Activity)

1. Shadowed/Supported at the counter when paying for entry
2. Numeracy support calculating cost of entry vs available to spend
3. Numeracy support regarding expected change
4. Numeracy support handling money or using card
5. Physical assistance required to dress/undress
6. Physical assistance required getting in/out of the pool
7. Reminded of safety around water
8. Reminded of appropriate behaviours in the water
9. Prompted to put valuables in their bag and take their bags to a safe space
10. Prompted when it was time to hop out of the pool to leave
11. Prompted to get changed into dry clothes
12. Reminded of pool etiquette, eg: no splashing people
13. Reminded to inform staff when moving to another area
14. Reminded of appropriate behaviour whilst out in the community

### 9. Basketball (Group/Solo Activity, Social Engagement, Personal Development, Health and Wellbeing)

1. Shadowed/Supported at the counter when paying for entry
2. Numeracy support calculating cost of entry vs available to spend
3. Numeracy support regarding expected change
4. Numeracy support handling money or using card
5. Prompted discussion regarding what the expectations for the day will be
6. Prompted to show understanding of expectations
7. Prompted respectful behaviours and interactions at all times
8. Prompted active and respectful listening
9. Reminded of appropriate behaviour in shared spaces/out in the community
10. Reminded to share the ball around
11. Verbal encouragement when lacking confidence
12. Reminded of stadium/court safety
13. Prompted appropriate interactions during activity
14. Reminded of respectful competitive behaviour
15. Demonstrated activity

### 10. `<bakehouse>` (Goal Focused Support, Social Engagement, Personal Development)

1. Prompted road safety from the car to the bakehouse
2. Prompted to wash and sanitise hands then put on an apron
3. Prompted to follow instructions precisely while stocking the fridge
4. Prompted sensible and safe behaviours in and around kitchen
5. Physical supports when washing dishes, carrying heavy items, stocking fridge
6. Direct verbal instructions on the task required: stocking fridge
7. Direct verbal instructions on the task required: carrying equipment
8. Direct verbal instructions on the task required: washing & putting away dishes
9. Direct verbal instructions on the task required: sweeping
10. Asked open-ended questions to encourage further learning, “Do you think that looks right to you?”
11. Encouraged and prompted to ask questions when unsure on the task to either CSW or Bakery staff
12. Reminded of the use of manners
13. Prompted active and respectful listening
14. Prompted respectful interactions at all times
15. Reminded of phone use at morning tea and lunch only

### 11. Woodwork (Group Activity, Social Engagement, Personal Development)

1. Prompted road safety from the bus to the workshop
2. Reminded of/Prompted appropriate behaviours in the community
3. Physical support to complete tasks, eg: sawing, sanding, drilling, using hammer and nails
4. Hand over hand support using equipment
5. Prompted active listening when being given instructions
6. Reminded to listen carefully to instruction and to follow them precisely (OHS)
7. Reminded to ask for clarification if unsure
8. Encouraged to start hammering
9. Verbal encouragement when using: saw
10. Verbal encouragement when using: drill
11. Verbal encouragement when using: hammer
12. Verbal encouragement when using: nails
13. Verbal encouragement when using: glue gun
14. Verbal encouragement when using: sandpaper
15. Verbal reminder regarding safety around tools
16. Prompted to stay on task
17. Prompted turn taking and patience
18. Verbally prompted personal space
19. Prompted to clean workspace
20. Verbal prompt to use inside voice
21. Prompted to keep hands away from equipment when not using it, eg: saw (OHS)
22. Hand over hand support as required
23. Reminded of phone use at morning tea and lunch only

### 12. Certificate II in Animal Care (Goal Focused Support, Group Activity, Social Engagement, Personal Development)

1. Verbal discussions on the topics
2. Asked open-ended questions to encourage further discussion
3. Encouraged to ask questions when unsure
4. Reminded to stay on topic
5. Reminded of respectful interactions
6. Prompted not to talk over other participants and wait their turn
7. Physical assistance/support during practical tasks
8. Role plays to demonstrate the topics being learnt
9. Reminded of phone use at morning tea and lunch only
10. Prompted to clean up their own spaces at the end of the session
11. Technology Support

### 13. Zumba (Group/Solo Activity, Social Engagement, Personal Development, Life Skills Development, Health and Wellbeing)

1. Prompted road safety in the carpark
2. Reminded to stay as a group and not race ahead
3. Supported/Shadowed to “check-in” for the Zumba Class
4. Verbal directions given on where to go for Zumba
5. Prompted to space out using arms making sure that they are not hitting anyone
6. Prompted to start stretching before dancing
7. Prompted paying attention to the demonstration by Gym staff
8. Prompted to follow instructions/demonstration from instructor
9. Prompted shared space etiquette in the community
10. Reminded to drink water regularly during Zumba session
11. Prompted to use sweat towel to wipe sweat
12. Reminded to stay on task
13. Encouraged to participate during all the songs
14. Prompted to be respectful of each other’s personal space
15. Encouraged respectful communication skills
16. Prompted to book for the next class independently

### 14. Gym @ `<gym>` or Outdoor Gym (Goal Focused Support, Physical Activity)

1. Reminded to make sure they have their required equipment before going to the gym. (eg. Drink bottle, gym towel etc.)
2. Prompted to store belongings safely
3. Prompted to do stretches prior to starting gym
4. Reminded to do a warm-up on a cardio machine
5. Support/Assistance getting on/off/using equipment
6. Physical demonstration on correct technique as and when required
7. Reminded of proper technique on equipment to limit risk of injury
8. Prompted shared space etiquette
9. Reminded about sanitising equipment after each use
10. Reminded to ensure all their belongings are taken before leaving
11. Reminded of appropriate behaviour whilst out in the community

### 15. Walk around the lake

No purpose tags.

1. Prompted to keep to the left to allow others to pass
2. Prompted shared space etiquette
3. Reminded to stick together as a group
4. Prompted to be respectful of others when walking
5. Reminded of safety around animals, to ask permission to approach and pat other walkers’ dogs

### 16. Cooking at `<base venue>`: Preparation (Goal Focused Support, Group Activity, Social Engagement, Personal Development)

1. Prompting turn taking while discussing ingredients
2. Prompted and supported online research of items
3. Prompted participation during class discussions
4. Prompted teamwork during ingredients check
5. Prompted drawing up a shopping list
6. Assisted drawing up a shopping list
7. Technology support to use phone/laptop to price ingredients
8. Literacy support to search the Coles website

### 17. Cooking at `<base venue>`: Shopping (Goal Focused Support, Group Activity, Social Engagement, Personal Development)

1. Prompted road safety to and from Coles/woollies
2. Prompted shared space etiquette in the community
3. Prompted to look for items independently
4. Support to find ingredients
5. Prompted to ask for support from staff independently when needed
6. Prompted to work together as a team
7. Shadowed at the counter while purchasing items in the event additional supports were required
8. Prompted to source the cheapest options
9. Assisted to find the cheapest options
10. Reminded of appropriate and respectful behaviour whilst out in the community

### 18. Cooking at `<base venue>`: Cooking (Goal Focused Support, Group Activity, Social Engagement, Personal Development)

1. Prompted to wash and sanitise hands pre-cooking
2. Prompted to don and doff gloves to prevent cross contamination
3. Literacy support when following recipe
4. Literacy support for conversion and understanding of measurements
5. Reminded of knife safety (OHS)
6. Prompted to follow instructions precisely (OHS)
7. Prompted turn taking
8. Physical/hand over hand support when: cutting
9. Physical/hand over hand support when: slicing
10. Physical/hand over hand support when: dicing
11. Physical/hand over hand support when: grating
12. Physical/hand over hand support when: stirring food
13. Physical/hand over hand support when using: onion chopper
14. Physical/hand over hand support when using: toasty maker
15. Physical/hand over hand support when using: blender
16. Physical/hand over hand support when using: air fryer
17. Physical/hand over hand support when using: microwave
18. Physical/hand over hand support when using: kettle
19. Physical/hand over hand support when using: pressure cooker
20. Reminded to focus when independently using: onion chopper
21. Reminded to focus when independently using: toasty maker
22. Reminded to focus when independently using: blender
23. Reminded to focus when independently using: air fryer
24. Reminded to focus when independently using: microwave
25. Reminded to focus when independently using: kettle
26. Reminded to focus when independently using: pressure cooker
27. Prompted sensible and safe behaviours in and around kitchen

### 19. `<church>` – Child Care Cooking Work Experience: Preparation (Goal Focused Support, Group Activity, Social Engagement, Personal Development)

The same items as Cooking at `<base venue>`: Preparation.

1. Prompting turn taking while discussing ingredients
2. Prompted and supported online research of items
3. Prompted participation during class discussions
4. Prompted teamwork during ingredients check
5. Prompted drawing up a shopping list
6. Assisted drawing up a shopping list
7. Technology support to use phone/laptop to price ingredients
8. Literacy support to search the Coles website

### 20. `<church>` – Child Care Cooking Work Experience: Cooking (Goal Focused Support, Group Activity, Social Engagement, Personal Development)

It differs from Cooking at `<base venue>`: Cooking in two places: an apron in place of gloves, and a last item about
phones.

1. Prompted to wash and sanitise hands pre-cooking
2. Prompted to don and doff apron to prevent cross contamination
3. Literacy support when following recipe
4. Literacy support for conversion and understanding of measurements
5. Reminded of knife safety (OHS)
6. Prompted to follow instructions precisely (OHS)
7. Prompted turn taking
8. Physical/hand over hand support when: cutting
9. Physical/hand over hand support when: slicing
10. Physical/hand over hand support when: dicing
11. Physical/hand over hand support when: grating
12. Physical/hand over hand support when: stirring food
13. Physical/hand over hand support when using: onion chopper
14. Physical/hand over hand support when using: toasty maker
15. Physical/hand over hand support when using: blender
16. Physical/hand over hand support when using: air fryer
17. Physical/hand over hand support when using: microwave
18. Physical/hand over hand support when using: kettle
19. Physical/hand over hand support when using: pressure cooker
20. Reminded to focus when independently using: onion chopper
21. Reminded to focus when independently using: toasty maker
22. Reminded to focus when independently using: blender
23. Reminded to focus when independently using: air fryer
24. Reminded to focus when independently using: microwave
25. Reminded to focus when independently using: kettle
26. Reminded to focus when independently using: pressure cooker
27. Prompted sensible and safe behaviours in and around kitchen
28. Reminded that phones are only used during morning tea or lunch time

### 21. `<sanctuary>` Wildlife Sanctuary (Goal Focused Support, Life Skills Development, Social Engagement, Personal Development)

1. Reminded of regular tasks
2. Verbal instructions on tasks when unsure
3. Prompted to maintain hand hygiene
4. Prompted to initiate regular tasks
5. Reminded to listen carefully to instructions and follow them precisely. (OHS)
6. Reminder that we are here to volunteer and complete tasks in a timely fashion – we were not here to only cuddle the
   animals
7. Physical assistance and support when feeding kangaroos and wombats
8. Reminded to be extra careful when walking around the animals. (OHS)
9. Prompted to concentrate and remaining focused on the task at hand
10. Reminded that animals could get sick/injured if there are lapses in concentration
11. Continuous prompting to take extra care outside due to very wet and slippery conditions
12. Reminded that phones are only used during morning tea or lunch time

### 22. `<men’s shed>` – Café (Goal Focused Support, Life Skills Development)

Two more lines on that day read as written for one participant and are left out.

1. Prompted to wash and sanitise hands pre-cooking
2. Prompted active listening when: being shown a new task
3. Prompted active listening when: speaking to staff
4. Prompted active listening when: speaking to customers
5. Prompted active listening when: listening to CSW
6. Role modelled tasks to be completed
7. Prompted to don and doff gloves to prevent cross contamination when working with food
8. Prompted to follow instructions precisely
9. Prompted to keep a safe distance from the hot stoves in the kitchen
10. Prompted sensible and safe behaviours in the kitchen, don’t touch food with bare hands, don’t eat food out of the
    serving bowl
11. Reminded to focus when using equipment, eg tongs, when handling food out of the pie warmer or Bain Marie
12. Reminded to close the glass sliding door of the Bain Marie once food had been put in
13. Prompting to clean up work area regularly
14. Prompting to assist with the dishes and cleaning up
15. Prompted to use respectful communication/manners when speaking to others
16. Prompted shared space etiquette in the kitchen – Saying “behind” when walking behind someone in the kitchen
17. Prompted to look for items independently
18. Prompted to ask for support independently when needed: from Chef
19. Prompted to ask for support independently when needed: from CSW
20. Prompted to work together as a team
21. Reminded to rinse the dishes before loading into the dishwasher
22. Prompted to clean up dishes and wipe down tables at the dining area
23. Reminded that phones are only used during morning tea or lunch time

### 23. `<pizza shop>` – Work Experience (Goal Focused Support, Group Activity, Social Engagement, Personal Development)

1. Prompted to wash and sanitise hands
2. Prompted to don and doff gloves to prevent cross contamination
3. Explained OHS protocols in relation to the dough mixer and dough roller
4. Role modelled tasks to be completed
5. Prompted to keep a safe distance from the hot oven
6. Reminded of knife safety
7. Reminded of safety around equipment
8. Prompted to follow instructions precisely
9. Prompted sensible and safe behaviours in and around kitchen
10. Prompted turn taking
11. Hand over hand support with dough roller as required
12. Prompted toilet timing
13. Reminded of personal space
14. Reminded that we only use phones during morning tea or lunch time
15. Prompted and supported clean-up of workspace before leaving

### 24. Equine Therapy (Goal Focused Support, Life Skills Development)

`<instructor>` stands for the instructor, whom the template names.

1. Verbally Prompted to listen to `<instructor>`
2. Verbally prompted to not interrupt when `<instructor>` is speaking
3. Prompted to hold questions till after `<instructor>` has explained the task
4. Verbally prompted to use respectful communication/manners when speaking to others
5. Encouraged using breathing techniques when becoming stressed, frightened
6. Prompted safety around horses
7. Encouraged use of work gloves
8. Physical assistance with putting on gloves
9. Prompted to stay focused
10. Literacy assistance given during end of session worksheet task
11. Physical assistance/support with guiding horses: to/from the arena
12. Physical assistance/support with guiding horses: to/from the round yard
13. Physical assistance/support with guiding horses: a walk along the road
14. Verbally prompted to clean up any mess
15. Encouraged to give new tasks a go
16. Prompted hand hygiene and use of sanitiser
17. Reminded to stay off phone during session

### 25. Woodwork – `<woodwork site>` (Group Activity, Social Engagement, Personal Development)

It differs from Woodwork in the tool list and in "Encouraged to start drilling".

1. Prompted road safety from the bus to the workshop
2. Reminded of/Prompted appropriate behaviours in the community
3. Physical support to complete tasks, eg: sawing, sanding, drilling, using hammer and nails
4. Hand over hand support using equipment
5. Prompted active listening when being given instructions
6. Reminded to listen carefully to instruction and to follow them precisely (OHS)
7. Reminded to ask for clarification if unsure
8. Encouraged to start drilling
9. Verbal encouragement when using: saw
10. Verbal encouragement when using: drill
11. Verbal encouragement when using: hammer
12. Verbal encouragement when using: nails
13. Verbal encouragement when using: bench clamp
14. Verbal encouragement when using: sander
15. Verbal reminder regarding safety around tools
16. Prompted to stay on task
17. Prompted turn taking and patience
18. Verbally prompted personal space
19. Prompted to clean workspace
20. Verbal prompt to use inside voice
21. Prompted to keep hands away from equipment when not using it, eg: saw (OHS)
22. Hand over hand support as required
23. Reminded of phone use at morning tea and lunch only

### 26. Photography Out and About (Group/Solo Activity, Social Engagement, Personal Development, Life Skills Development)

1. Prompted road safety
2. Verbal directions given on which way to walk
3. Prompted shared space etiquette in the community
4. Prompted to take photos
5. Reminded to ensure a straight horizon
6. Encouraged to think outside the box
7. Reminded “framing” a photo
8. Reminded to have the focal object to the side and not always in the middle to tell the story
9. Reminded to have fun and be creative
10. Prompted to be respectful of each other’s personal space
11. Encouraged respectful communication skills
12. Reminded to stay as a group and not race ahead

### 27. Bowling (Social Engagement, Personal Development)

1. Supported/Shadowed at the counter when purchasing bowling game
2. Numeracy support calculating cost of game vs money available to spend
3. Numeracy support regarding expected change
4. Numeracy support handling money or using card
5. Supported/Shadowed to choose an appropriately sized ball to use
6. Reminded of respectful competitive behaviour
7. Prompted turn taking
8. Provided verbal encouragement
9. Reminded of appropriate behaviour whilst out in the community
10. Prompted to return bowling balls to the correct spot
11. Prompted to sanitise hands after use
12. Reminded of phone use at morning tea and lunch only

### 28. `<base venue>` Art (Group Activity, Social Engagement, Personal Development)

1. Verbal directions on what was available for today’s activity
2. Encouraged to watch a video tutorial/explanation of the project for the day
3. Discussion about what materials to purchase
4. Discussion about costs involved to stay within project budget
5. Support/Shadowed when shopping in the community
6. Reminded of appropriate behaviours when out in the community
7. Reminded of road safety: awareness with `<bus name>` bus travel and car park safety
8. Reminded of road safety: awareness when walking to the shop
9. Physical assistance to carry purchases
10. Physical assistance required to use art/craft tools: hot glue gun
11. Physical assistance required to use art/craft tools: paint brushes
12. Physical assistance required to use art/craft tools: canvas
13. Physical assistance required to use art/craft tools: light box
14. Physical assistance to complete the task
15. Technology support to use a laptop and printer
16. Verbal directions on where to find required materials
17. Prompted active listening
18. Prompted to use art/craft materials with care
19. Prompted to persevere with project
20. Prompted to stay on task
21. Provided verbal encouragement
22. Reminded to not waste supplies
23. Prompted to complete current project before starting on a new one
24. Prompted to clean up their own spaces at the end of each activity
25. Reminded of phone use at morning tea and lunch only

### 29. Market Stall @ `<market square>`

No purpose tags.

1. Physical assistance required to unload/load the bus/car with stock
2. Physical assistance required to set up/pack up the tables and set out and display stock
3. Shadowed/Supported to count out the $50 float
4. Discussion required about how to serve a customer
5. Prompted to greet customers
6. Numeracy support to use the calculator to add up the customers purchases
7. Prompted to always use manners when talking to customers
8. Money handling support to receive money and give change
9. Prompted to thank the customer for their purchase

### 30. Baking

No purpose tags.

1. Prompted to wash and sanitise hands pre-cooking
2. Prompted to don and doff gloves to prevent cross contamination
3. Literacy support when following recipe
4. Literacy support for conversion and understanding of measurements
5. Reminded of knife safety (OHS)
6. Prompted to follow instructions precisely (OHS)
7. Prompted turn taking

### 31. Activities of choice (Goal Focused Support, Group/Solo Activity, Social Engagement, Personal Development)

1. Verbal suggestions regarding which activities are available
2. Physical assistance gathering the required materials
3. Prompted to clean up workspace at the end of the day
4. Prompted to put equipment back where everything belonged
5. Technology support: Online Searches
6. Technology support: Printing
7. Technology support: Setting up the light box
8. Technology support: Sorting diamond dots

### 32. Further Supports Provided (details to be provided in support notes)

None needs detail: the heading sends the detail to Daily Support Notes.

1. Emotional Support
2. Behaviour Management Strategies
3. Communication Support
4. Choice and Control Support
5. Personal Care Support
6. Independence Building Support
7. De-Escalation Strategies
8. Skill Development Encouragement Provided
9. Additional Safety Supervision Provided
10. Additional Physical Assistance/Support Provided
11. Ambulant Support Provided

## After entering it

- Check the groups and items against a Word day, and write down any wording that does not match, for the owner
  (first-use step 229).
- Switch the Medication box on for the participants who have a medication arrangement at the program, in each
  participant's Details; the skill never does (SKILL.md rule 7).
- Set each goal's term when the goals are entered (Short term unless the Word template puts the goal under Medium or
  Long term goal).
