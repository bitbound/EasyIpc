﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace EasyIpc
{
    [DataContract]
    public enum MessageType
    {
        [EnumMember]
        Unspecified,
        [EnumMember]
        Send,
        [EnumMember]
        Invoke,
        [EnumMember]
        Response
    }
}
