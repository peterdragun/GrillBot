﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Grillbot.Database.Entity
{
    [Table("Channelstats")]
    public class ChannelStat
    {
        [Key]
        [Column]
        [Required]
        [StringLength(32)]
        public string ID { get; set; }

        [Column]
        public long Count { get; set; }

        [Column]
        public DateTime LastMessageAt { get; set; } = DateTime.MinValue;

        [NotMapped]
        public ulong SnowflakeID
        {
            get => Convert.ToUInt64(ID);
            set => ID = value.ToString();
        }

        [Column]
        [Required]
        [StringLength(30)]
        public string GuildID { get; set; }

        [NotMapped]
        public ulong GuildIDSnowflake
        {
            get => Convert.ToUInt64(GuildID);
            set => GuildID = value.ToString();
        }
    }
}
