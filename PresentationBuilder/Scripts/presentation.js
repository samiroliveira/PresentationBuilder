﻿var Presentations = {

	download: function (id)
	{
		if ($('#download-form').length < 1)
		{
			$('<form>').attr({
				method: 'POST',
				id: 'download-form',
				action: urlBase + "api/PresentationsAPI/download/" + id
			}).appendTo('body');
		}
		else
		{
			$('#download-form').attr('action', urlBase + "api/PresentationsAPI/download/" + id);
			$('#download-form').html('');
		}

		$('#download-form').submit();
	}
}