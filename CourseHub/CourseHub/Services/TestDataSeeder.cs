using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CourseHub.Services
{
    // Dev-only convenience seeder: creates a batch of ready-to-use test accounts (already
    // email-confirmed, with roles and profile data filled in) so you can log in and test
    // without going through Register + OTP 25 times. Runs once - each account is only
    // created if an account with that email doesn't already exist, so it's safe to leave
    // this wired up and just restart the app; it won't create duplicates.
    public static class TestDataSeeder
    {
        public const string TestPassword = "Parola123!";

        private class TestUserSpec
        {
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
            public string Role { get; set; } = ""; // "Teach" or "Learn"
            public int Age { get; set; }
            public string Gender { get; set; } = "";
            public string? EducationLevel { get; set; }
            public string? TeachingLevel { get; set; }
            public int[] TopicIds { get; set; } = Array.Empty<int>();
            public string Bio { get; set; } = "";
        }

        private static readonly List<TestUserSpec> Specs = new()
        {
            // ===== Teachers (12) =====
            new() { Username = "Ana_Ionescu", Email = "ana.ionescu@test.com", Role = "Teach", Age = 34, Gender = "Female", TeachingLevel = "Advanced", TopicIds = new[] { 1, 3 }, Bio = "Predau matematică și programare de peste 8 ani." },
            new() { Username = "Mihai_Popescu", Email = "mihai.popescu@test.com", Role = "Teach", Age = 41, Gender = "Male", TeachingLevel = "Professional", TopicIds = new[] { 2 }, Bio = "Profesor de fizică și chimie, pasionat de experimente." },
            new() { Username = "Elena_Dumitrescu", Email = "elena.dumitrescu@test.com", Role = "Teach", Age = 29, Gender = "Female", TeachingLevel = "Intermediate", TopicIds = new[] { 4 }, Bio = "Te ajut să înveți engleză și franceză rapid și eficient." },
            new() { Username = "Andrei_Constantinescu", Email = "andrei.constantinescu@test.com", Role = "Teach", Age = 27, Gender = "Male", TeachingLevel = "Expert", TopicIds = new[] { 3 }, Bio = "Dezvoltator software full-time, predau C# și Python." },
            new() { Username = "Maria_Stanciu", Email = "maria.stanciu@test.com", Role = "Teach", Age = 38, Gender = "Female", TeachingLevel = "Beginner", TopicIds = new[] { 5 }, Bio = "Pictură și desen pentru începători, într-un ritm relaxat." },
            new() { Username = "Radu_Georgescu", Email = "radu.georgescu@test.com", Role = "Teach", Age = 45, Gender = "Male", TeachingLevel = "Advanced", TopicIds = new[] { 6 }, Bio = "Antreprenor și mentor în marketing și management." },
            new() { Username = "Ioana_Marinescu", Email = "ioana.marinescu@test.com", Role = "Teach", Age = 31, Gender = "Female", TeachingLevel = "Professional", TopicIds = new[] { 1, 2 }, Bio = "Matematică și științe, cu accent pe examenele naționale." },
            new() { Username = "Cristian_Nistor", Email = "cristian.nistor@test.com", Role = "Teach", Age = 33, Gender = "Male", TeachingLevel = "Intermediate", TopicIds = new[] { 3, 6 }, Bio = "Programare și antreprenoriat tech." },
            new() { Username = "Diana_Vasile", Email = "diana.vasile@test.com", Role = "Teach", Age = 26, Gender = "Female", TeachingLevel = "Expert", TopicIds = new[] { 4, 5 }, Bio = "Limbi străine și arte vizuale, combinate creativ." },
            new() { Username = "Bogdan_Florescu", Email = "bogdan.florescu@test.com", Role = "Teach", Age = 50, Gender = "Male", TeachingLevel = "Beginner", TopicIds = new[] { 6 }, Bio = "30 de ani de experiență în afaceri, acum predau." },
            new() { Username = "Alexandra_Munteanu", Email = "alexandra.munteanu@test.com", Role = "Teach", Age = 36, Gender = "Female", TeachingLevel = "Advanced", TopicIds = new[] { 2 }, Bio = "Biologie și chimie explicate simplu și practic." },
            new() { Username = "Vlad_Radulescu", Email = "vlad.radulescu@test.com", Role = "Teach", Age = 40, Gender = "Male", TeachingLevel = "Professional", TopicIds = new[] { 1 }, Bio = "Meditații de matematică pentru liceu și facultate." },

            // ===== Students (13) =====
            new() { Username = "Andreea_Barbu", Email = "andreea.barbu@test.com", Role = "Learn", Age = 17, Gender = "Female", EducationLevel = "High School", TopicIds = new[] { 1 }, Bio = "Vreau să mă pregătesc pentru bac la matematică." },
            new() { Username = "Gabriel_Toma", Email = "gabriel.toma@test.com", Role = "Learn", Age = 20, Gender = "Male", EducationLevel = "University Student", TopicIds = new[] { 3 }, Bio = "Student la Automatică, vreau să învăț programare avansată." },
            new() { Username = "Larisa_Croitoru", Email = "larisa.croitoru@test.com", Role = "Learn", Age = 19, Gender = "Female", EducationLevel = "Some College", TopicIds = new[] { 4 }, Bio = "Îmi place să învăț limbi noi, în special franceza." },
            new() { Username = "Stefan_Dobre", Email = "stefan.dobre@test.com", Role = "Learn", Age = 16, Gender = "Male", EducationLevel = "High School", TopicIds = new[] { 2, 5 }, Bio = "Pasionat de știință și desen deopotrivă." },
            new() { Username = "Simona_Enache", Email = "simona.enache@test.com", Role = "Learn", Age = 24, Gender = "Female", EducationLevel = "Graduate Student", TopicIds = new[] { 6 }, Bio = "Master în Business, vreau să-mi deschid propria afacere." },
            new() { Username = "Cosmin_Iliescu", Email = "cosmin.iliescu@test.com", Role = "Learn", Age = 22, Gender = "Male", EducationLevel = "Self-Taught", TopicIds = new[] { 3 }, Bio = "Autodidact, învăț programare de acasă de un an." },
            new() { Username = "Roxana_Petrescu", Email = "roxana.petrescu@test.com", Role = "Learn", Age = 21, Gender = "Female", EducationLevel = "University Student", TopicIds = new[] { 1, 3 }, Bio = "Studentă la Informatică, caut ajutor la algoritmi." },
            new() { Username = "Daniel_Anghel", Email = "daniel.anghel@test.com", Role = "Learn", Age = 27, Gender = "Male", EducationLevel = "PhD", TopicIds = new[] { 2 }, Bio = "Doctorand în fizică, caut discuții avansate pe domeniu." },
            new() { Username = "Carmen_Neagu", Email = "carmen.neagu@test.com", Role = "Learn", Age = 15, Gender = "Female", EducationLevel = "High School", TopicIds = new[] { 5 }, Bio = "Vreau să învăț să pictez în acuarelă." },
            new() { Username = "Paul_Barbulescu", Email = "paul.barbulescu@test.com", Role = "Learn", Age = 29, Gender = "Male", EducationLevel = "College Graduate", TopicIds = new[] { 6, 1 }, Bio = "Vreau să trec de la inginerie la antreprenoriat." },
            new() { Username = "Oana_Ciobanu", Email = "oana.ciobanu@test.com", Role = "Learn", Age = 18, Gender = "Female", EducationLevel = "Some College", TopicIds = new[] { 4 }, Bio = "Vreau să dau un examen de germană anul acesta." },
            new() { Username = "Marius_Zamfir", Email = "marius.zamfir@test.com", Role = "Learn", Age = 23, Gender = "Male", EducationLevel = "University Student", TopicIds = new[] { 2, 3 }, Bio = "Student la Robotică, caut mentori tehnici." },
            new() { Username = "Adriana_Lupu", Email = "adriana.lupu@test.com", Role = "Learn", Age = 25, Gender = "Non-Binary", EducationLevel = "Graduate Student", TopicIds = new[] { 5, 4 }, Bio = "Studiez arte și limbi în paralel, pasionat de traduceri literare." },
        };

        public static async Task SeedAsync(IServiceProvider services, ILogger logger)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var context = services.GetRequiredService<ApplicationDbContext>();

            var createdByEmail = new Dictionary<string, ApplicationUser>();
            int createdCount = 0;

            foreach (var spec in Specs)
            {
                var existing = await userManager.FindByEmailAsync(spec.Email);
                if (existing != null)
                {
                    createdByEmail[spec.Email] = existing;
                    continue;
                }

                var user = new ApplicationUser
                {
                    UserName = spec.Username,
                    Email = spec.Email,
                    EmailConfirmed = true, // skip the OTP flow entirely for these accounts
                    Role = spec.Role,
                    Age = spec.Age,
                    Gender = spec.Gender,
                    EducationLevel = spec.EducationLevel,
                    TeachingLevel = spec.TeachingLevel,
                    SelectedTopics = string.Join(",", spec.TopicIds),
                    Bio = spec.Bio
                };

                var result = await userManager.CreateAsync(user, TestPassword);
                if (!result.Succeeded)
                {
                    logger.LogWarning("TestDataSeeder: failed to create '{Email}': {Errors}",
                        spec.Email, string.Join("; ", result.Errors.Select(e => e.Description)));
                    continue;
                }

                await userManager.AddToRoleAsync(user, "User");
                await userManager.AddToRoleAsync(user, spec.Role);

                createdByEmail[spec.Email] = user;
                createdCount++;
            }

            if (createdCount > 0)
            {
                logger.LogInformation("TestDataSeeder: created {Count} test accounts (password for all: '{Password}').",
                    createdCount, TestPassword);
            }

            // Seed a handful of hire requests / conversations so Connections, Hire Requests,
            // and Chat aren't empty on a fresh database either.
            await SeedConnectionsAsync(context, createdByEmail, logger);
        }

        private static async Task SeedConnectionsAsync(
            ApplicationDbContext context,
            Dictionary<string, ApplicationUser> usersByEmail,
            ILogger logger)
        {
            if (await context.HireRequests.AnyAsync())
            {
                return; // already have data (either seeded before, or real usage) - don't touch it
            }

            ApplicationUser? U(string email) => usersByEmail.TryGetValue(email, out var u) ? u : null;

            var pairs = new (string teacherEmail, string studentEmail, bool accepted)[]
            {
                ("ana.ionescu@test.com", "gabriel.toma@test.com", true),
                ("andrei.constantinescu@test.com", "roxana.petrescu@test.com", true),
                ("elena.dumitrescu@test.com", "larisa.croitoru@test.com", true),
                ("ioana.marinescu@test.com", "andreea.barbu@test.com", true),
                ("mihai.popescu@test.com", "daniel.anghel@test.com", false), // left pending on purpose
            };

            var addedRequests = new List<HireRequest>();
            foreach (var (teacherEmail, studentEmail, accepted) in pairs)
            {
                var teacher = U(teacherEmail);
                var student = U(studentEmail);
                if (teacher == null || student == null) continue;

                addedRequests.Add(new HireRequest
                {
                    RequesterId = student.Id,
                    TargetId = teacher.Id,
                    IsAccepted = accepted,
                    RequestedAt = DateTime.Now.AddDays(-Random.Shared.Next(1, 20))
                });
            }

            context.HireRequests.AddRange(addedRequests);
            await context.SaveChangesAsync();

            // A couple of sample text conversations for the accepted connections.
            var ana = U("ana.ionescu@test.com");
            var gabriel = U("gabriel.toma@test.com");
            if (ana != null && gabriel != null)
            {
                context.Messages.AddRange(
                    new Message { SenderId = gabriel.Id, ReceiverId = ana.Id, Content = "Bună! Aș vrea ajutor la algoritmi de sortare.", Timestamp = DateTime.Now.AddDays(-3), IsRead = true },
                    new Message { SenderId = ana.Id, ReceiverId = gabriel.Id, Content = "Sigur, putem începe cu bubble sort și quicksort. Când ești disponibil?", Timestamp = DateTime.Now.AddDays(-3).AddMinutes(5), IsRead = true },
                    new Message { SenderId = gabriel.Id, ReceiverId = ana.Id, Content = "Mâine seară merge?", Timestamp = DateTime.Now.AddDays(-2), IsRead = false }
                );
            }

            var andrei = U("andrei.constantinescu@test.com");
            var roxana = U("roxana.petrescu@test.com");
            if (andrei != null && roxana != null)
            {
                context.Messages.AddRange(
                    new Message { SenderId = roxana.Id, ReceiverId = andrei.Id, Content = "Bună ziua! Am o întrebare despre recursivitate.", Timestamp = DateTime.Now.AddDays(-1), IsRead = true },
                    new Message { SenderId = andrei.Id, ReceiverId = roxana.Id, Content = "Bună! Trimite-mi exemplul care nu-ți iese și te ajut.", Timestamp = DateTime.Now.AddDays(-1).AddMinutes(10), IsRead = false }
                );
            }

            await context.SaveChangesAsync();
            logger.LogInformation("TestDataSeeder: seeded {Count} hire requests and sample conversations.", addedRequests.Count);
        }
    }
}
