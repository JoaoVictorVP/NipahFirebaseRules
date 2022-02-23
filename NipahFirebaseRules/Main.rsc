// Matches determinate location on db
match Some/$Location {
	// Yes, anyone can write and read
	write => true;
	read => true;
	
	// Matches subdomain on Some/$Location called OtherLocation, the final path should look like Some/$Location/OtherLocation
	match OtherLocation {
		// But no, nobody can write or read this
		write => false;
		read => false;
	}
	
	match AnotherOne {
		// But yeah, only the great admin can write and read from this location
		write => invoke isAdmin();
		read => invoke isAdmin();
	}
}

// Define a variable globally, yes, this syntax is strange but should work for now
function AdminVar() {
	let admin => "3289052589239058"
}

// Verify if logged user is the admin one
function isAdmin() {
	if exp !(auth.uid) == @admin
}