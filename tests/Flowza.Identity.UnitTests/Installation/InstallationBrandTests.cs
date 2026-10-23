using Flowza.Identity.Application.Installation;

namespace Flowza.Identity.UnitTests.Installation;

/// <summary>T003: the installation name and its initials mark (UI-08, C6, C7).</summary>
public sealed class InstallationBrandTests
{
    [Theory]
    [InlineData("State Bank", "SB")] // two words
    [InlineData("State Bank of Example", "SB")] // many words: only the first two count
    [InlineData("Flowza", "FL")] // one word: its first two letters
    [InlineData("X", "X")] // one letter
    [InlineData("acme bank", "AB")] // capitals
    [InlineData("  Acme \t  Bank  ", "AB")] // extra spaces and tabs
    [InlineData("भारतीय सेवा", "भासे")] // Hindi: a consonant with its vowel sign stays one letter
    [InlineData("भारत", "भार")] // one Hindi word
    [InlineData("தமிழ் நாடு", "தநா")] // Tamil
    [InlineData("école Lumière", "ÉL")] // a letter with a combining accent stays whole
    public void UI_08_AC1_initials_are_the_first_letters_of_the_first_two_words(string name, string initials)
    {
        InstallationBrand.InitialsOf(name).ShouldBe(initials);
        InstallationBrand.Create(name).Value.Initials.ShouldBe(initials);
    }

    [Fact]
    public void UI_08_AC1_the_name_is_kept_as_written_without_outer_spaces()
    {
        var brand = InstallationBrand.Create("  State Bank of Example ").Value;

        brand.Name.ShouldBe("State Bank of Example");
    }

    [Fact]
    public void UI_08_AC1_a_100_character_name_is_kept_whole()
    {
        var name = new string('a', 99) + "Z";

        InstallationBrand.Create(name).Value.Name.ShouldBe(name);
    }

    [Fact]
    public void UI_08_AC1_length_counts_characters_as_people_see_them()
    {
        // 100 Hindi syllables of two code units each: 200 UTF-16 characters, but 100 letters on screen.
        var name = string.Concat(Enumerable.Repeat("भा", InstallationBrand.MaxNameLength));

        InstallationBrand.Create(name).IsSuccess.ShouldBeTrue();
        InstallationBrand.Create(name + "भा").IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void UI_08_a_blank_name_is_refused(string? name)
    {
        InstallationBrand.Create(name).Error.ShouldBe(InstallationBrand.InvalidName);
    }

    [Fact]
    public void UI_08_a_101_character_name_is_refused()
    {
        InstallationBrand.Create(new string('a', 101)).Error.ShouldBe(InstallationBrand.InvalidName);
    }

    [Fact]
    public void UI_08_AC2_the_default_name_is_Flowza_with_FL()
    {
        var brand = InstallationBrand.Create(InstallationBrand.DefaultName).Value;

        brand.Name.ShouldBe("Flowza");
        brand.Initials.ShouldBe("FL");
    }
}
