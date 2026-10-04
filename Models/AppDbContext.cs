using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using WebsiteCreditHoursSystem.Models;

namespace WebsiteCreditHoursSystem.Models
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<CourseOffering> CourseOfferings { get; set; }
        public DbSet<CourseRegistration> CourseRegistrations { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Grade> Grades { get; set; }
        public DbSet<Instructor> Instructors { get; set; }
        public DbSet<ScheduleLecture> Schedule { get; set; }
        public DbSet<SystemUser> SystemUsers { get; set; }
        public DbSet<PasswordResetCode> PasswordResetCodes { get; set; }
        public DbSet<CoursePrerequisite> CoursePrerequisites { get; set; }
        public DbSet<SystemControl> SystemControls { get; set; }
        
        public DbSet<User> Users { get; set; }
        public DbSet<StudentSemesterResult> StudentSemesterResults { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // -----------------------
            // PasswordResetCode -> SystemUser
            // -----------------------
            modelBuilder.Entity<PasswordResetCode>()
                .HasOne(pr => pr.SystemUser)
                .WithMany(u => u.ResetCodes)   // لو عايزة تضيفي navigation property
                .HasForeignKey(pr => pr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // CourseOffering -> Course
            modelBuilder.Entity<CourseOffering>()
                .HasOne(o => o.Course)
                .WithMany(c => c.CourseOfferings)
                .HasForeignKey(o => o.CourseID)
                .OnDelete(DeleteBehavior.Restrict);



            modelBuilder.Entity<CourseOffering>()
               .HasOne(co => co.User)
               .WithMany()
               .HasForeignKey(co => co.UserID)
               .OnDelete(DeleteBehavior.Restrict);


            // CourseRegistration -> CourseOffering
            modelBuilder.Entity<CourseRegistration>()
                .HasOne(r => r.CourseOffering)
                .WithMany(o => o.CourseRegistrations)
                .HasForeignKey(r => r.OfferingID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Grade>()
                .Property(g => g.GradePoints)
                .HasPrecision(5, 2);
           
            // الربط بين الدكتور والمستخدم
            modelBuilder.Entity<Instructor>()
                .HasOne(i => i.User)
                .WithOne()
                .HasForeignKey<Instructor>(i => i.UserId);
            modelBuilder.Entity<Student>()
       .Property(s => s.GPA);


            modelBuilder.Entity<Student>()
                .Property(s => s.CompletedCreditHours)
               ;

            // إضافة القيمة الافتراضية للـ Semester (ترم أول)
            modelBuilder.Entity<Student>()
                .Property(s => s.Semester)
                ; // لاحظي لو الحقل string بنحط "1" لو int بنحط 1
        }
    }
    }



