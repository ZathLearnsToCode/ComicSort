﻿// <auto-generated />
using System;
using ComicSort.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ComicSort.DataAccess.Migrations.LibraryDB
{
    [DbContext(typeof(LibraryDBContext))]
    [Migration("20210604225831_LibraryInitial")]
    partial class LibraryInitial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "5.0.6");

            modelBuilder.Entity("ComicSort.Domain.Models.ComicSortLibraries", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Created")
                        .HasColumnType("TEXT");

                    b.Property<string>("LastAccessed")
                        .HasColumnType("TEXT");

                    b.Property<string>("LibraryFile")
                        .HasColumnType("TEXT");

                    b.Property<string>("LibraryName")
                        .HasColumnType("TEXT");

                    b.Property<string>("LibraryPath")
                        .HasColumnType("TEXT");

                    b.Property<string>("LibraryType")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Libraries");
                });
#pragma warning restore 612, 618
        }
    }
}
