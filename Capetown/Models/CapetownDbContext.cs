using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;


namespace Capetown.Models
{
    class CapetownDbContext : DbContext
    {
        private readonly IConfigurationRoot _config;
        public static CapetownDbContext GetInstance(IConfigurationRoot config)
        {
            return new CapetownDbContext(config, new DbContextOptions<CapetownDbContext>());
        }

        public CapetownDbContext(IConfigurationRoot config, DbContextOptions<CapetownDbContext> options) : base( options )
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _config = config;
        }

        #region Overrides of DbContext

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_config.GetConnectionString("DefaultConnection"));
        }

        #endregion

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            #region Модели сущностей
            
            builder.Entity<Store>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(36);
                entity.Property(e => e.OwnerId).IsRequired();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Latitude);
                entity.Property(e => e.Longitude);
                entity.Ignore(e => e.Location);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.IsDelete).HasColumnType("timestamp with time zone");
                entity.Property(e => e.Updated).HasColumnType("timestamp with time zone");
                entity.Property(e => e.Created).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");

                entity.ToTable("Stores");
            });
            
            #endregion
        }
        public virtual DbSet<Store> Stores { get; set; }
        
    }
}
