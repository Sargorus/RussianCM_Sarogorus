# Chat window radio wrap (prefix and postfix)
# CMU14 Radio Begin: radio chat shows quoted speech without automatic sender identity.
# chat-radio-message-wrap = [color={$color}][font={$fontType} size={$fontSize}]{$channel} [bold]{$name}[/bold] {$verb}, { chat-manager-speech-double-quote-begin }{$message}{ chat-manager-speech-double-quote-end }[/font][/color]
# chat-radio-message-wrap-bold = [color={$color}][font={$fontType} size={$fontSize}]{$channel} [bold]{$name}[/bold] {$verb}, [bold]{ chat-manager-speech-double-quote-begin }{$message}{ chat-manager-speech-double-quote-end }[/bold][/font][/color]
chat-radio-message-wrap = [color={$color}][font={$fontType} size={$fontSize}]{$channel} { chat-manager-speech-double-quote-begin }{$message}{ chat-manager-speech-double-quote-end }[/font][/color]
chat-radio-message-wrap-bold = [color={$color}][font={$fontType} size={$fontSize}]{$channel} [bold]{ chat-manager-speech-double-quote-begin }{$message}{ chat-manager-speech-double-quote-end }[/bold][/font][/color]
# CMU14 End
examine-headset-default-channel = Use {$prefix} for the default channel ([color={$color}]{$channel}[/color]).

chat-radio-common = Common
chat-radio-centcom = CentComm
chat-radio-command = Command
chat-radio-engineering = Engineering
chat-radio-medical = Medical
chat-radio-science = Science
chat-radio-security = Security
chat-radio-service = Service
chat-radio-supply = Supply
chat-radio-syndicate = Syndicate
chat-radio-freelance = Freelance

# not headset but whatever
chat-radio-handheld = Handheld
chat-radio-binary = Binary
chat-radio-xenoborg = Xenoborg
chat-radio-mothership = Mothership
