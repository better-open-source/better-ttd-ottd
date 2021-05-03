const { UpdateFrequencies, UpdateTypes } = require("node-openttd-admin/enums");

var ottd = require("node-openttd-admin"),
  ottdConnection =  new ottd.connection();
 
ottdConnection.connect("localhost", 3977);
 
ottdConnection.on('connect', function(){
  ottdConnection.authenticate("BetterTTD-Bot", "12345");
});
ottdConnection.on('welcome', function(data){
  ottdConnection.send_rcon("say \"hello world\"");
  ottdConnection.send_update_frequency(UpdateTypes.CHAT, UpdateFrequencies.AUTOMATIC);
  //ottdConnection.close();
});
ottdConnection.on('chat', function(data) {
  console.log(data);
});